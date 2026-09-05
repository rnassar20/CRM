using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Crm.Api.Data;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c> / <c>database update</c>.
/// Lets the tooling build the model without executing the app's top-level startup logic
/// (secret validation, automatic migration, seeding), so migrations can be generated/listed
/// from a clean host. The connection string here is never used at runtime — it only needs to
/// be parseable so the provider is configured; DB access isn't required to scaffold a migration.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Runtime maps DateTime to naive local timestamps via the legacy switch in Program.cs;
        // the design-time model must reflect the same mapping so a scaffolded migration doesn't
        // emit spurious timestamp-with-time-zone alters.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Host=localhost;Database=crm_db;Username=crm;Password=design-time";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cs)
            .Options;
        return new AppDbContext(options);
    }
}
