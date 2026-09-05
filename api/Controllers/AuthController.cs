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

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var lockKey = $"login-lock:{email}";
        if (cache.TryGetValue(lockKey, out _))
            return LockedOut();

        // Unified model: find person credential by username (email) or by person email
        var cred = await db.PersonCredentials
            .Include(c => c.Person)
            .FirstOrDefaultAsync(c => c.Username == email);
        if (cred is null)
            cred = await db.PersonCredentials
                .Include(c => c.Person)
                .FirstOrDefaultAsync(c => c.Person != null && c.Person.Email == email);
        if (cred is null || cred.Person is null || cred.Person.Status != "1"
            || !BCrypt.Net.BCrypt.Verify(request.Password, cred.PasswordHash))
        {
            RecordFailedAttempt(email);
            await db.LoginAttempts.AddAsync(new LoginAttempt
            {
                PersonId = cred?.PersonId,
                Email = email,
                IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Success = false,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return Unauthorized("Invalid email or password.");
        }

        try
        {
            cache.Remove(lockKey);
            cache.Remove(FailsKey(email));
            await db.LoginAttempts.AddAsync(new LoginAttempt
            {
                PersonId = cred.PersonId,
                Email = email,
                IpAddress = Request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Success = true,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();

            var userDto = new UserDto(
                cred.Person.Id,
                $"{cred.Person.FirstName} {cred.Person.LastName}".Trim(),
                cred.Person.Email ?? email,
                cred.AccessLevel == 1 ? "Admin" : "Agent",
                cred.Person.Status == "1",
                cred.Person.CreatedAt);

            var (token, expires) = jwt.CreateToken(userDto);
            return Ok(new AuthResponse(token, expires, userDto));
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
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

    [HttpPost("register")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var hasUsers = await db.PersonCredentials.AnyAsync();
        var currentRole = User.FindFirstValue("role");

        if (hasUsers && currentRole != "Admin")
            return Forbid();

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.PersonCredentials.AnyAsync(c => c.Username == email))
            return BadRequest("Email already registered.");

        var role = hasUsers ? "Agent" : "Admin";
        var accessLevel = role == "Admin" ? (short)1 : (short)2;

        // Create person + credential together
        var person = new Person
        {
            FirstName = request.FullName.Trim().Split(' ')[0],
            LastName = string.Join(' ', request.FullName.Trim().Split(' ').Skip(1)),
            Email = email,
            Status = "1",
            PersonType = 11, // Employee
            ProfileId = 1, // default employee profile (phcyid=1 in ew_profile)
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Persons.Add(person);
        await db.SaveChangesAsync();

        var cred = new PersonCredential
        {
            PersonId = person.Id,
            Username = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            AccessLevel = accessLevel,
            MustReset = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.PersonCredentials.Add(cred);
        await db.SaveChangesAsync();

        var userDto = new UserDto(person.Id, request.FullName.Trim(), email, role, true, person.CreatedAt);
        var (token, expires) = jwt.CreateToken(userDto);
        return Ok(new AuthResponse(token, expires, userDto));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = User.GetUserId();
        var cred = await db.PersonCredentials
            .Include(c => c.Person)
            .FirstOrDefaultAsync(c => c.PersonId == id);
        if (cred is null || cred.Person is null) return NotFound();
        return Ok(new UserDto(
            cred.Person.Id,
            $"{cred.Person.FirstName} {cred.Person.LastName}".Trim(),
            cred.Person.Email ?? cred.Username,
            cred.AccessLevel == 1 ? "Admin" : "Agent",
            cred.Person.Status == "1",
            cred.Person.CreatedAt));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var id = User.GetUserId();
        var cred = await db.PersonCredentials
            .Include(c => c.Person)
            .FirstOrDefaultAsync(c => c.PersonId == id);
        if (cred is null || cred.Person is null) return NotFound();
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, cred.PasswordHash))
            return BadRequest("Current password is incorrect.");
        if (request.NewPassword.Length < 8)
            return BadRequest("New password must be at least 8 characters.");

        cred.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        cred.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public record ChangePasswordRequest([Required] string CurrentPassword, [Required, MinLength(8)] string NewPassword);
