using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, JwtTokenService jwt, IMemoryCache cache) : ControllerBase
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Brute-force hardened login:
    ///  - rate-limited by remote IP (Policy "login", 10 requests / 15 min, enforced by middleware),
    ///  - per-account lockout after 5 failed attempts for 15 minutes (in-memory, resets on restart).
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var lockKey = $"login-lock:{email}";
        if (cache.TryGetValue(lockKey, out _))
            return LockedOut();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            RecordFailedAttempt(email);
            return Unauthorized("Invalid email or password.");
        }

        cache.Remove(lockKey);
        cache.Remove(FailsKey(email));
        var (token, expires) = jwt.CreateToken(user);
        return Ok(new AuthResponse(token, expires,
            new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt)));
    }

    private void RecordFailedAttempt(string email)
    {
        var failsKey = FailsKey(email);
        var attempts = cache.GetOrCreate(failsKey, e =>
        {
            e.SetAbsoluteExpiration(LockoutWindow);
            return 0;
        });
        attempts++;
        cache.Set(failsKey, attempts, LockoutWindow);

        if (attempts >= MaxFailedAttempts)
            cache.Set($"login-lock:{email}", true, LockoutWindow);
    }

    private static string FailsKey(string email) => $"login-fails:{email}";

    private ActionResult LockedOut() => new ObjectResult("Too many failed attempts. Please try again later.")
    {
        StatusCode = StatusCodes.Status429TooManyRequests
    };

    /// <summary>
    /// The very first registered account becomes Admin automatically.
    /// Afterwards only Admins can create accounts.
    /// </summary>
    [HttpPost("register")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var hasUsers = await db.Users.AnyAsync();
        var currentRole = User.FindFirstValue("role");

        if (hasUsers && currentRole != UserRole.Admin.ToString())
            return Forbid();

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return BadRequest("Email already registered.");

        var role = hasUsers ? UserRole.Agent : UserRole.Admin;
        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (token, expires) = jwt.CreateToken(user);
        return Ok(new AuthResponse(token, expires,
            new UserDto(user.Id, user.FullName, user.Email, role.ToString(), true, user.CreatedAt)));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = User.GetUserId();
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        return Ok(new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user is null) return NotFound();
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest("Current password is incorrect.");
        if (request.NewPassword.Length < 8)
            return BadRequest("New password must be at least 8 characters.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record ChangePasswordRequest([Required] string CurrentPassword, [Required, MinLength(8)] string NewPassword);
