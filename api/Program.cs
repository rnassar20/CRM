using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Crm.Api;
using Crm.Api.Controllers;
using Crm.Api.Data;
using Crm.Api.Models;
using Crm.Api.Services;
using Crm.Api.Services.WhatsApp;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Npgsql 6+ maps DateTime to "timestamp with time zone" and only accepts UTC.
// The CRM stores naive/local dates (expiry days, agenda times), so opt into the lenient mapping.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // enums travel as strings ("Pharmacy", "Subscribed", ...) both directions
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep raw claim types: sub / name / role
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"] ?? "")),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    })
    // Browser session used only by the Hangfire dashboard (separate from the API's JWT).
    .AddCookie(DashboardAuthController.CookieScheme, o =>
    {
        o.Cookie.Name = "crm_dashboard_auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.LoginPath = "/dashboard/login";
        o.AccessDeniedPath = "/dashboard/login?error=Admin+role+required";
    });
builder.Services.AddAuthorization();

// application services
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<ILicenseKeyService, LicenseKeyService>();
builder.Services.AddScoped<ReminderJobs>();
builder.Services.AddSingleton<LoggingWhatsAppSender>();
builder.Services.AddHttpClient<MetaCloudWhatsAppSender>();
builder.Services.AddScoped<IWhatsAppSender>(sp =>
{
    var provider = sp.GetRequiredService<IConfiguration>()["WhatsApp:Provider"] ?? "Logging";
    return provider.Equals("MetaCloud", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<MetaCloudWhatsAppSender>()
        : sp.GetRequiredService<LoggingWhatsAppSender>();
});

// Hangfire replaces the ReminderWorker BackgroundService. Storage lives in the same
// Postgres database (hangfire.* schema) so no new infrastructure is required.
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(
        // Resolve the connection string lazily so test hosts (which override config after
        // service registration) and env-injected secrets are picked up when the server starts.
        o => o.UseConnectionFactory(new ConfigConnectionFactory(() => builder.Configuration.GetConnectionString("Default"))),
        new PostgreSqlStorageOptions
        {
            // keep Hangfire's internal tables out of the app's "public" schema
            SchemaName = "hangfire"
        }));
builder.Services.AddHangfireServer(o =>
{
    // reminder sends are throttled by the WhatsApp provider; don't fan out 5*CPU jobs
    o.WorkerCount = 2;
    o.SchedulePollingInterval = TimeSpan.FromSeconds(15);
});

// Per-client-IP throttling for the login endpoint (brute-force protection).
// Fixed window: 10 attempts per 15 minutes. Partitioned by remote IP address.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0
            }));
    // Anonymous key-validation endpoint (called by desktop ERP tooling): permissive but not unlimited.
    o.AddPolicy("validate", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();

const string CorsPolicy = "web";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var cid = CorrelationIdMiddleware.CorrelationId(context);
    using (logger.BeginScope(new Dictionary<string, object>(1) { ["RequestId"] = cid }))
    {
        await next(context);
    }
});

ValidateRequiredSecrets(app.Configuration);

// migrate database + seed demo data on first run
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Databases created earlier via EnsureCreated lack the migrations history table.
    // Create it, then baseline legacy schemas at the initial migration so Migrate()
    // adopts them as-is and only applies newer deltas - no data loss.
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
            "MigrationId" character varying(150) NOT NULL,
            "ProductVersion" character varying(32) NOT NULL,
            CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId"));
        """);
    var hasLegacySchema = db.Database
        .SqlQuery<int>($"SELECT COUNT(*)::int FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users'")
        .AsEnumerable().First() > 0;
    var hasAnyApplied = db.Database
        .SqlQuery<int>($"SELECT COUNT(*)::int FROM \"__EFMigrationsHistory\"")
        .AsEnumerable().First() > 0;
    if (hasLegacySchema && !hasAnyApplied)
    {
        var firstPending = db.Database.GetPendingMigrations().First();
        var efVersion = typeof(DbContext).Assembly.GetName().Version!.ToString(3);
        db.Database.ExecuteSqlInterpolated(
            $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({firstPending}, {efVersion})");
    }

    db.Database.Migrate();
    if (app.Environment.IsDevelopment())
        DbSeeder.Seed(db);
}

// Register the periodic jobs the old ReminderWorker loop used to run.
// AddOrUpdate is idempotent by id, so restarting the app never queues duplicates.
using (var scope = app.Services.CreateScope())
{
    var recurring = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurring.AddOrUpdate<ReminderJobs>("expiry-reminders", j => j.RunExpiryRemindersAsync(), Cron.HourInterval(6));
    recurring.AddOrUpdate<ReminderJobs>("follow-up-processing", j => j.RunFollowUpProcessingAsync(), Cron.HourInterval(6));
}

// Central error handling: unhandled exceptions become RFC 7807 ProblemDetails (no stack traces).
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        if (context.Response.HasStarted) throw;
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "An unexpected error occurred.",
            status = StatusCodes.Status500InternalServerError
        }, context.RequestAborted);
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard: jobs, retries, recurring schedules, and server status.
// Restricted to authenticated Admin users via a cookie session (see DashboardAuthController).
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireDashboardAuthFilter()]
});
app.MapControllers();

app.Run();

/// <summary>Fails fast at startup when required secrets are missing or still placeholders.</summary>
static void ValidateRequiredSecrets(IConfiguration config)
{
    var errors = new List<CheckError>();

    var conn = config["ConnectionStrings:Default"];
    if (string.IsNullOrWhiteSpace(conn))
        errors.Add(new CheckError("ConnectionStrings:Default", "ConnectionStrings__Default",
            "database connection string is missing (set ConnectionStrings__Default)"));

    CheckSecret(config, "Jwt:Secret", "JWT signing secret", "Jwt__Secret", 64, errors);
    CheckSecret(config, "Licensing:Secret", "license master secret", "Licensing__Secret", 16, errors);

    if (errors.Count > 0 && !Environment.GetEnvironmentVariable("Testing_SkipSecretValidation").Equals("1", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "Refusing to start: required secrets are missing or set to a placeholder. " +
            "Set them via environment variables before running.\n  - " +
            string.Join("\n  - ", errors.Select(e => $"{e.Key}: {e.Reason} (env {e.Env})")));

    static void CheckSecret(IConfiguration cfg, string key, string what, string env, int minLength, List<CheckError> errors)
    {
        var value = cfg[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new CheckError(key, env, $"{what} is missing (set {env})"));
        }
        else if (value.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
                 && !cfg.GetValue<bool>("Testing:AllowPlaceholderSecrets", false))
        {
            errors.Add(new CheckError(key, env, $"{what} is still set to a placeholder value"));
        }
        else if (value.Length < minLength)
        {
            errors.Add(new CheckError(key, env, $"{what} must be at least {minLength} characters"));
        }
    }
}

// If the app is configured to use the real WhatsApp provider, fail fast when its
// credentials are missing or still placeholders — otherwise the app starts "working"
// but sends nothing, which is worse than refusing to start.
{
    var provider = builder.Configuration["WhatsApp:Provider"]?.Trim();
    if (provider is not null && provider.Equals("MetaCloud", StringComparison.OrdinalIgnoreCase))
    {
        static string Resolve(IConfiguration cfg, string key) => cfg[key]?.Trim() ?? "";
        var accessToken = Resolve(builder.Configuration, "WhatsApp:MetaCloud:AccessToken");
        var phoneNumberId = Resolve(builder.Configuration, "WhatsApp:MetaCloud:PhoneNumberId");
        var wErrors = new List<CheckError>();
        if (string.IsNullOrEmpty(accessToken))
            wErrors.Add(new CheckError("WhatsApp:MetaCloud:AccessToken", "WhatsApp__MetaCloud__AccessToken",
                "Meta Cloud access token is missing (set WhatsApp__MetaCloud__AccessToken)"));
        else if (accessToken.Contains("change_me", StringComparison.OrdinalIgnoreCase)
                 || accessToken.Contains("DEV_ONLY", StringComparison.OrdinalIgnoreCase)
                 || accessToken.Length < 10)
            wErrors.Add(new CheckError("WhatsApp:MetaCloud:AccessToken", "WhatsApp__MetaCloud__AccessToken",
                "Meta Cloud access token looks like a placeholder"));
        if (string.IsNullOrEmpty(phoneNumberId))
            wErrors.Add(new CheckError("WhatsApp:MetaCloud:PhoneNumberId", "WhatsApp__MetaCloud__PhoneNumberId",
                "Meta Cloud phone number ID is missing (set WhatsApp__MetaCloud__PhoneNumberId)"));
        else if (phoneNumberId.Contains("change_me", StringComparison.OrdinalIgnoreCase)
                 || phoneNumberId.Contains("DEV_ONLY", StringComparison.OrdinalIgnoreCase))
            wErrors.Add(new CheckError("WhatsApp:MetaCloud:PhoneNumberId", "WhatsApp__MetaCloud__PhoneNumberId",
                "Meta Cloud phone number ID looks like a placeholder"));
        if (wErrors.Count > 0)
            throw new InvalidOperationException(
                "Refusing to start: WhatsApp provider is set to MetaCloud but its credentials are missing or placeholders.\n  - " +
                string.Join("\n  - ", wErrors.Select(e => $"{e.Key}: {e.Reason} (env {e.Env})")));
    }
}

internal record CheckError(string Key, string Env, string Reason);

/// <summary>Creates a fresh Npgsql connection per call using a lazily-resolved connection string.</summary>
file sealed class ConfigConnectionFactory(Func<string?> connectionString) : Hangfire.PostgreSql.IConnectionFactory
{
    public NpgsqlConnection GetOrCreateConnection()
    {
        var cs = connectionString()
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured for Hangfire storage");
        return new NpgsqlConnection(cs);
    }
}

/// <summary>
/// Allows only authenticated Admin users onto the Hangfire dashboard. Unauthenticated requests
/// are redirected to the cookie login page at /dashboard/login (the redirect is deferred to
/// OnStarting so it isn't overwritten by the dashboard middleware's 401).
/// </summary>
file sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();

        // UseAuthentication only runs the default (JWT) scheme, so authenticate the
        // dashboard cookie scheme explicitly here.
        var auth = http.AuthenticateAsync(DashboardAuthController.CookieScheme).GetAwaiter().GetResult();
        if (auth.Succeeded && auth.Principal.IsInRole(UserRole.Admin.ToString()))
            return true;

        var path = http.Request.Path.Value;
        var returnUrl = Uri.EscapeDataString(string.IsNullOrEmpty(path) ? "/hangfire" : path);
        var destination = auth.Succeeded
            ? $"/dashboard/login?error={Uri.EscapeDataString("Admin role required")}" // logged in but not an Admin
            : $"/dashboard/login?ReturnUrl={returnUrl}";                             // not logged in at all
        http.Response.OnStarting(() =>
        {
            http.Response.Redirect(destination, false);
            return Task.CompletedTask;
        });
        return false;
    }
}

public partial class Program;
