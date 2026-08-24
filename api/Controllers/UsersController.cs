using System.ComponentModel.DataAnnotations;
using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<UserRole> ValidRoles = [UserRole.Admin, UserRole.Agent];

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll()
    {
        var users = await db.Users.OrderBy(u => u.FullName).ToListAsync();
        return Ok(users.Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role.ToString(), u.IsActive, u.CreatedAt)).ToList());
    }

    /// <summary>Active users for assignment dropdowns (tickets / agenda).</summary>
    [HttpGet("agents")]
    public async Task<ActionResult<object>> GetAgents()
    {
        var agents = await db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, Role = u.Role.ToString() })
            .ToListAsync();
        return Ok(agents);
    }

    public record CreateUserRequest(
        [Required, MaxLength(200)] string FullName,
        [Required, EmailAddress] string Email,
        [Required, MinLength(8)] string Password,
        [Required] string Role);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request)
    {
        if (!Enum.TryParse<UserRole>(request.Role, true, out var role) || !ValidRoles.Contains(role))
            return BadRequest($"Unknown role '{request.Role}'. Allowed: Admin, Agent.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return BadRequest("Email already registered.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new UserDto(user.Id, user.FullName, user.Email, role.ToString(), true, user.CreatedAt));
    }

    [HttpPatch("{id:int}/toggle-active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Id == User.GetUserId()) return BadRequest("You cannot deactivate your own account.");

        user.IsActive = !user.IsActive;
        await db.SaveChangesAsync();
        return NoContent();
    }

    public record ResetPasswordRequest([Required, MinLength(8)] string NewPassword);

    [HttpPatch("{id:int}/reset-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest request)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
