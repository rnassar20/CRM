using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/followups")]
[Authorize]
public class FollowUpsController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<FollowUpStatus> ValidStatuses = [FollowUpStatus.Pending, FollowUpStatus.Done, FollowUpStatus.Missed, FollowUpStatus.Cancelled];

    /// <summary>Agenda view: filter by range/status/user.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FollowUpDto>>> GetAll(
        [FromQuery] int? clientId,
        [FromQuery] int? userId,
        [FromQuery] bool mineOnly = false,
        [FromQuery] string? status = "Pending",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var now = DateTime.Now;
        var query = db.FollowUps.AsNoTracking().AsQueryable();

        if (clientId is { } cid) query = query.Where(f => f.ClientId == cid);
        if (userId is { } uid) query = query.Where(f => f.AssignedToId == uid);
        if (mineOnly) query = query.Where(f => f.AssignedToId == User.GetUserId());
        if (status == "Pending")
            query = query.Where(f => f.Status == FollowUpStatus.Pending);
        else if (Enum.TryParse<FollowUpStatus>(status, true, out var fs) && fs != FollowUpStatus.Pending)
            query = query.Where(f => f.Status == fs);
        // status omitted -> all

        if (from is { } f1) query = query.Where(x => x.ScheduledAt >= f1);
        if (to is { } t1) query = query.Where(x => x.ScheduledAt <= t1);

        var items = await query
            .Include(f => f.Client).Include(f => f.AssignedTo)
            .OrderBy(f => f.ScheduledAt < now ? 0 : 1)   // overdue first
            .ThenBy(f => f.ScheduledAt)
            .Take(200)
            .ToListAsync();

        return Ok(items.Select(Mappers.ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateFollowUpRequest request)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == request.ClientId))
            return BadRequest($"Client {request.ClientId} not found.");
        var assignedToId = request.AssignedToId ?? User.GetUserId();
        if (!await db.Users.AnyAsync(u => u.Id == assignedToId))
            return BadRequest($"User {assignedToId} not found.");

        var followUp = new FollowUp
        {
            ClientId = request.ClientId,
            Title = request.Title.Trim(),
            Description = request.Description,
            ScheduledAt = request.ScheduledAt,
            AssignedToId = assignedToId,
            CreatedById = User.GetUserId()
        };
        db.FollowUps.Add(followUp);
        await db.SaveChangesAsync();
        return Ok(new { followUp.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateFollowUpRequest request)
    {
        var followUp = await db.FollowUps.FindAsync(id);
        if (followUp is null) return NotFound();

        if (request.Title is not null) followUp.Title = request.Title.Trim();
        if (request.Description is not null) followUp.Description = request.Description;
        if (request.ScheduledAt is { } when) followUp.ScheduledAt = when;

        if (request.Status is not null)
        {
            if (!Enum.TryParse<FollowUpStatus>(request.Status, true, out var s) || !ValidStatuses.Contains(s))
                return BadRequest($"Unknown status '{request.Status}'.");
            followUp.Status = s;
        }

        if (request.AssignedToId is { } uid)
        {
            if (!await db.Users.AnyAsync(u => u.Id == uid)) return BadRequest($"User {uid} not found.");
            followUp.AssignedToId = uid;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var followUp = await db.FollowUps.FindAsync(id);
        if (followUp is null) return NotFound();
        followUp.Status = FollowUpStatus.Done;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var followUp = await db.FollowUps.FindAsync(id);
        if (followUp is null) return NotFound();
        followUp.Status = FollowUpStatus.Cancelled;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
