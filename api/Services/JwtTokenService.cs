using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Crm.Api.Dtos;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Services;

public class JwtTokenService(IConfiguration config)
{
    public (string Token, DateTime ExpiresAtUtc) CreateToken(UserDto user)
    {
        var secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is not configured");
        var minutes = int.TryParse(config["Jwt:ExpireMinutes"], out var m) ? m : 720;
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new[]
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("email", user.Email),
            new Claim("name", user.FullName),
            new Claim("role", user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow.AddSeconds(-5),
            expires: expiresAtUtc,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
