using Crm.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Api.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    static CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default",
            "Host=localhost;Port=5433;Database=crm_db;Username=crm;Password=crm@dock123");
        Environment.SetEnvironmentVariable("Jwt__Secret",
            "crm-test-jwt-signing-secret-minimum-sixty-four-characters-long-ok!!");
        Environment.SetEnvironmentVariable("Licensing__Secret",
            "baaf945deabc60b9706001bce0898126534aac3bf64b184d");
        Environment.SetEnvironmentVariable("Testing_SkipSecretValidation", "1");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "crm-test-jwt-signing-secret-minimum-sixty-four-characters-long-ok!!",
                ["Jwt:Issuer"] = "crm-api",
                ["Jwt:Audience"] = "crm-web",
                ["Jwt:ExpireMinutes"] = "720",
                ["Licensing:Secret"] = "baaf945deabc60b9706001bce0898126534aac3bf64b184d",
                ["Testing:AllowPlaceholderSecrets"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            var dbDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor is not null)
                services.Remove(dbDescriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql("Host=localhost;Port=5433;Database=crm_db;Username=crm;Password=crm@dock123"));
        });
    }
}
