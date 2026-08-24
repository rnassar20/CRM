using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, JwtTokenService jwt) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("Invalid email or password.");

        var (token, expires) = jwt.CreateToken(user);
        return Ok(new AuthResponse(token, expires,
            new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString(), user.IsActive, user.CreatedAt)));
    }

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
