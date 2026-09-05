using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Crm.Api.Dtos;
using Crm.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.UnitTests;

public class JwtTokenServiceTests
{
    private const string Secret = "crm-test-jwt-signing-secret-minimum-sixty-four-characters-long-ok!!";

    private static JwtTokenService CreateService(int expireMinutes = 720)
        => new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = Secret,
                ["Jwt:Issuer"] = "crm-api",
                ["Jwt:Audience"] = "crm-web",
                ["Jwt:ExpireMinutes"] = expireMinutes.ToString()
            })
            .Build());

    private static readonly UserDto User = new(
        Id: 42,
        FullName: "Jane Agent",
        Email: "jane@crm.local",
        Role: "Agent",
        IsActive: true,
        CreatedAt: DateTime.UtcNow);

    [Fact]
    public void CreateToken_ProducesValidJwt_WithExpectedClaims()
    {
        var svc = CreateService();
        var (token, expiresUtc) = svc.CreateToken(User);

        expiresUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(720), TimeSpan.FromSeconds(30));
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("crm-api");
        jwt.Audiences.Should().Contain("crm-web");
        jwt.Claims.First(c => c.Type == "sub").Value.Should().Be("42");
        jwt.Claims.First(c => c.Type == "email").Value.Should().Be("jane@crm.local");
        jwt.Claims.First(c => c.Type == "name").Value.Should().Be("Jane Agent");
        jwt.Claims.First(c => c.Type == "role").Value.Should().Be("Agent");
    }

    [Fact]
    public void CreateToken_IsSignedWithSecret_AndVerifies()
    {
        var svc = CreateService();
        var (token, _) = svc.CreateToken(User);

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Secret));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "crm-api",
            ValidateAudience = true,
            ValidAudience = "crm-web",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validated);
        validated.Should().NotBeNull();
        // Inbound claim mapping converts sub -> nameidentifier and role -> role by default.
        principal.FindFirstValue(ClaimTypes.NameIdentifier).Should().Be("42");
        principal.FindFirstValue(ClaimTypes.Role).Should().Be("Agent");
    }

    [Fact]
    public void CreateToken_TamperedToken_IsRejected()
    {
        var svc = CreateService();
        var (token, _) = svc.CreateToken(User);

        var parts = token.Split('.');
        // Tamper with the SIGNATURE (last segment). The payload stays intact so the token is
        // still well-formed JSON, but the signature no longer matches → signature validation fails.
        var signature = parts[2];
        var tamperedSignature = signature[..^1] + (signature[^1] == 'A' ? 'B' : 'A');
        var tampered = $"{parts[0]}.{parts[1]}.{tamperedSignature}";

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Secret));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "crm-api",
            ValidateAudience = true,
            ValidAudience = "crm-web",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        Action act = () => new JwtSecurityTokenHandler().ValidateToken(tampered, parameters, out _);
        act.Should().Throw<SecurityTokenException>();
    }

    [Fact]
    public void CreateToken_RespectsExpireMinutes()
    {
        var svc = CreateService(expireMinutes: 15);
        var (token, expiresUtc) = svc.CreateToken(User);

        expiresUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(10));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.ValidTo.Should().BeCloseTo(expiresUtc, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CreateToken_MissingSecret_Throws()
    {
        var svc = new JwtTokenService(new ConfigurationBuilder().Build());
        Action act = () => svc.CreateToken(User);
        act.Should().Throw<InvalidOperationException>();
    }
}
