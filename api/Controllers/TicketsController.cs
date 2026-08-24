using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<TicketPriority> ValidPriorities = [TicketPriority.Low, TicketPriority.Medium, TicketPriority.High, TicketPriority.Critical];
    private static readonly HashSet<TicketStatus> ValidStatuses = [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed];

    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketDto>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] int? clientId,
        [FromQuery] int? assignedToId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Tickets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(t => EF.Functions.ILike(t.Title, $"%{term}%"));
        }
        if (Enum.TryParse<TicketStatus>(status, true, out var st) && ValidStatuses.Contains(st)) query = query.Where(t => t.Status == st);
        if (Enum.TryParse<TicketPriority>(priority, true, out var pr) && ValidPriorities.Contains(pr)) query = query.Where(t => t.Priority == pr);
        if (clientId is { } cid) query = query.Where(t => t.ClientId == cid);
        if (assignedToId is { } uid) query = query.Where(t => t.AssignedToId == uid);

        var total = await query.CountAsync();
        var tickets = await query
            .Include(t => t.Client).Include(t => t.AssignedTo).Include(t => t.CreatedBy)
            .OrderByDescending(t => t.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var ticketIds = tickets.Select(t => t.Id).ToList();
        var counts = await db.TicketComments
            .Where(c => ticketIds.Contains(c.TicketId))
            .GroupBy(c => c.TicketId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return Ok(new PagedResult<TicketDto>(
            tickets.Select(t => t.ToDto(counts.GetValueOrDefault(t.Id))).ToList(),
            total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<object>> GetById(int id)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .Include(t => t.Client).Include(t => t.AssignedTo).Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket is null) return NotFound();

        var comments = await db.TicketComments.AsNoTracking()
            .Include(c => c.User)
            .Where(c => c.TicketId == id)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        return Ok(new { ticket = ticket.ToDto(comments.Count), comments = comments.Select(Mappers.ToDto).ToList() });
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateTicketRequest request)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == request.ClientId))
            return BadRequest($"Client {request.ClientId} not found.");
        var priority = ParsePriority(request.Priority);
        if (priority is null) return BadRequest($"Unknown priority '{request.Priority}'.");
        if (request.AssignedToId is { } uid && !await UserExists(uid)) return BadRequest($"User {uid} not found.");

        var ticket = new Ticket
        {
            ClientId = request.ClientId,
            Title = request.Title.Trim(),
            Description = request.Description,
            Priority = priority.Value,
            Status = TicketStatus.Open,
            AssignedToId = request.AssignedToId,
            CreatedById = User.GetUserId()
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        ticket.Client = await db.Clients.FirstAsync(c => c.Id == ticket.ClientId);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, new { ticket.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTicketRequest request)
    {
        var ticket = await db.Tickets.FindAsync(id);
        if (ticket is null) return NotFound();

        if (request.Title is not null) ticket.Title = request.Title.Trim();
        if (request.Description is not null) ticket.Description = request.Description;

        if (request.Priority is not null)
        {
            var p = ParsePriority(request.Priority);
            if (p is null) return BadRequest($"Unknown priority '{request.Priority}'.");
            ticket.Priority = p.Value;
        }

        if (request.Status is not null)
        {
            if (!Enum.TryParse<TicketStatus>(request.Status, true, out var s) || !ValidStatuses.Contains(s))
                return BadRequest($"Unknown status '{request.Status}'.");
            ticket.Status = s;
            ticket.ResolvedAt = s is TicketStatus.Resolved or TicketStatus.Closed ? DateTime.UtcNow : null;
        }

        if (request.Unassign) ticket.AssignedToId = null;
        else if (request.AssignedToId is { } uid)
        {
            if (!await UserExists(uid)) return BadRequest($"User {uid} not found.");
            ticket.AssignedToId = uid;
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/comments")]
    public async Task<ActionResult<TicketCommentDto>> AddComment(int id, AddCommentRequest request)
    {
        var exists = await db.Tickets.AnyAsync(t => t.Id == id);
        if (!exists) return NotFound();

        var comment = new TicketComment
        {
            TicketId = id,
            UserId = User.GetUserId(),
            Body = request.Body.Trim(),
            IsInternal = request.IsInternal
        };
        db.TicketComments.Add(comment);

        var ticket = await db.Tickets.FirstAsync(t => t.Id == id);
        ticket.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await db.Entry(comment).Reference(c => c.User).LoadAsync();
        return Ok(comment.ToDto());
    }

    private static TicketPriority? ParsePriority(string value)
        => Enum.TryParse<TicketPriority>(value, true, out var p) && ValidPriorities.Contains(p) ? p : null;

    private Task<bool> UserExists(int userId) => db.Users.AnyAsync(u => u.Id == userId && u.IsActive);
}
