using System.Text;
using System.Text.Json.Serialization;
using Crm.Api.Data;
using Crm.Api.Services;
using Crm.Api.Services.WhatsApp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

const string CorsPolicy = "web";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
        db.Database.ExecuteSqlRaw(
            $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{firstPending}', '{efVersion}')");
    }

    db.Database.Migrate();
    DbSeeder.Seed(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
