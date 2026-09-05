using System.ComponentModel.DataAnnotations;
using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

/// <summary>
/// Administration of staff accounts. Staff are <see cref="Person"/>s with PersonType=11
/// (Employee) paired with a <see cref="PersonCredential"/> (username + BCrypt password + access
/// level). This is the same backing data the JWT login (<see cref="AuthController"/>) authenticates
/// against, so users created/deactivated here are the ones that can actually sign in.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(AppDbContext db) : ControllerBase
{
    private const short PersonTypeStaff = 11;
    private const int DefaultProfileId = 1; // default employee profile (phcyid=1 in ew_profile)
    private const short AccessLevelAdmin = 1;
    private const short AccessLevelAgent = 2;
    private static readonly HashSet<UserRole> ValidRoles = [UserRole.Admin, UserRole.Agent];

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll()
    {
        var staff = await db.Persons
            .AsNoTracking()
            .Include(p => p.Credential)
            .Where(p => p.PersonType == PersonTypeStaff && p.Credential != null)
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .ToListAsync();

        return Ok(staff.Select(ToUserDto).ToList());
    }

    /// <summary>Active staff for assignment dropdowns (tickets / agenda).</summary>
    [HttpGet("agents")]
    public async Task<ActionResult<object>> GetAgents()
    {
        var agents = await db.Persons
            .AsNoTracking()
            .Where(p => p.PersonType == PersonTypeStaff && p.Status == "1" && p.Credential != null)
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName)
            .Select(p => new
            {
                p.Id,
                FullName = $"{p.FirstName} {p.LastName}".Trim(),
                Role = p.Credential!.AccessLevel == AccessLevelAdmin ? "Admin" : "Agent"
            })
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
        if (await db.PersonCredentials.AnyAsync(c => c.Username == email)
            || await db.Persons.AnyAsync(p => p.Email == email))
            return BadRequest("Email already registered.");

        var accessLevel = role == UserRole.Admin ? AccessLevelAdmin : AccessLevelAgent;
        var (firstName, lastName) = SplitName(request.FullName);

        var person = new Person
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Status = "1",
            PersonType = PersonTypeStaff,
            ProfileId = DefaultProfileId,
            CreatedBy = User.GetUserId(),
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

        return CreatedAtAction(nameof(GetAll), ToUserDto(person, cred));
    }

    [HttpPatch("{id:int}/toggle-active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var person = await db.Persons
            .Include(p => p.Credential)
            .FirstOrDefaultAsync(p => p.Id == id && p.PersonType == PersonTypeStaff && p.Credential != null);
        if (person is null) return NotFound();
        if (person.Id == User.GetUserId()) return BadRequest("You cannot deactivate your own account.");

        person.Status = person.Status == "1" ? "0" : "1"; // active flag; login checks Status == "1"
        person.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    public record ResetPasswordRequest([Required, MinLength(8)] string NewPassword);

    [HttpPatch("{id:int}/reset-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest request)
    {
        var person = await db.Persons
            .Include(p => p.Credential)
            .FirstOrDefaultAsync(p => p.Id == id && p.PersonType == PersonTypeStaff && p.Credential != null);
        if (person is null || person.Credential is null) return NotFound();

        person.Credential.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        person.Credential.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Splits "First Last" into (first, last), tolerating a single token / extra spaces.</summary>
    private static (string First, string Last) SplitName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1
            ? (parts[0], string.Join(' ', parts[1..]))
            : (parts.Length == 1 ? parts[0] : "", "");
    }

    private static UserDto ToUserDto(Person person) => ToUserDto(person, person.Credential);

    private static UserDto ToUserDto(Person person, PersonCredential? cred)
        => new(
            person.Id,
            new[] { person.FirstName, person.MiddleName, person.LastName }
                .Where(x => !string.IsNullOrWhiteSpace(x)).Aggregate("", (a, b) => a + " " + b).Trim(),
            person.Email ?? cred?.Username ?? "",
            cred?.AccessLevel == AccessLevelAdmin ? "Admin" : "Agent",
            person.Status == "1",
            person.CreatedAt);
}
