using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Crm.Api.Data;
using Crm.Api.Services;
using Crm.Api.Services.WhatsApp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

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
    });
builder.Services.AddAuthorization();

// application services
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<ILicenseKeyService, LicenseKeyService>();
builder.Services.AddSingleton<LoggingWhatsAppSender>();
builder.Services.AddHttpClient<MetaCloudWhatsAppSender>();
builder.Services.AddScoped<IWhatsAppSender>(sp =>
{
    var provider = sp.GetRequiredService<IConfiguration>()["WhatsApp:Provider"] ?? "Logging";
    return provider.Equals("MetaCloud", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<MetaCloudWhatsAppSender>()
        : sp.GetRequiredService<LoggingWhatsAppSender>();
});
builder.Services.AddHostedService<ReminderWorker>();

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
        .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'Users'")
        .AsEnumerable().First() > 0;
    var hasAnyApplied = db.Database
        .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM \"__EFMigrationsHistory\"")
        .AsEnumerable().First() > 0;
    if (hasLegacySchema && !hasAnyApplied)
    {
        var firstPending = db.Database.GetPendingMigrations().First();
        var efVersion = typeof(DbContext).Assembly.GetName().Version!.ToString(3);
        db.Database.ExecuteSqlInterpolated(
            $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({firstPending}, {efVersion})");
    }

    db.Database.Migrate();
    DbSeeder.Seed(db);
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

    if (errors.Count > 0)
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
        else if (value.Contains("change_me", StringComparison.OrdinalIgnoreCase)
                 || value.Contains("DEV_ONLY", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(new CheckError(key, env, $"{what} is still set to a placeholder value"));
        }
        else if (value.Length < minLength)
        {
            errors.Add(new CheckError(key, env, $"{what} must be at least {minLength} characters"));
        }
    }
}

internal record CheckError(string Key, string Env, string Reason);

public partial class Program;
