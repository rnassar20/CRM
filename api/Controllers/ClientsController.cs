using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientsController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<ClientType> ValidTypes = [ClientType.Pharmacy, ClientType.GiftShop, ClientType.DoctorClinic, ClientType.Hospital, ClientType.Other];
    private static readonly HashSet<ClientStatus> ValidStatuses = [ClientStatus.Potential, ClientStatus.Contacted, ClientStatus.Interested, ClientStatus.NotInterested, ClientStatus.Subscribed];

    /// <summary>Clients list with contact info + latest subscription status.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemDto>>> GetAll(
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Clients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{term}%") ||
                EF.Functions.ILike(c.Phone, $"%{term}%") ||
                EF.Functions.ILike(c.ContactPerson, $"%{term}%"));
        }
        if (Enum.TryParse<ClientType>(type, true, out var t)) query = query.Where(c => c.Type == t);
        if (Enum.TryParse<ClientStatus>(status, true, out var s)) query = query.Where(c => c.Status == s);

        var total = await query.CountAsync();

        var clients = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var ids = clients.Select(c => c.Id).ToList();
        var clientSubs = await db.Subscriptions
            .AsNoTracking()
            .Include(sub => sub.Plan)
            .Where(sub => ids.Contains(sub.ClientId))
            .ToListAsync();
        var latestSubs = clientSubs
            .GroupBy(sub => sub.ClientId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(sub => sub.ExpiryDate).First());

        var items = clients.Select(c =>
        {
            latestSubs.TryGetValue(c.Id, out var sub);
            return new ClientListItemDto(
                c.Id, c.Name, c.ContactPerson, c.Phone, c.Email, c.City,
                c.Type.ToString(), c.Status.ToString(),
                sub?.Id, sub?.Plan.Name, sub?.ExpiryDate, sub?.PaymentStatus.ToString());
        }).ToList();

        return Ok(new PagedResult<ClientListItemDto>(items, total, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientDetailDto>> GetById(int id)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (client is null) return NotFound();

        var subs = await db.Subscriptions.AsNoTracking()
            .Include(x => x.Plan).Include(x => x.Client)
            .Where(x => x.ClientId == id).OrderByDescending(x => x.StartDate).ToListAsync();
        var interactions = await db.Interactions.AsNoTracking()
            .Include(x => x.Client).Include(x => x.User)
            .Where(x => x.ClientId == id).OrderByDescending(x => x.CreatedAt).Take(50).ToListAsync();
        var tickets = await db.Tickets.AsNoTracking()
            .Include(x => x.Client).Include(x => x.AssignedTo).Include(x => x.CreatedBy)
            .Where(x => x.ClientId == id).OrderByDescending(x => x.UpdatedAt).ToListAsync();
        var followUps = await db.FollowUps.AsNoTracking()
            .Include(x => x.Client).Include(x => x.AssignedTo)
            .Where(x => x.ClientId == id).OrderByDescending(x => x.ScheduledAt).Take(50).ToListAsync();

        return Ok(new ClientDetailDto(
            client.Id, client.Name, client.ContactPerson, client.Phone, client.Email,
            client.Address, client.City, client.Type.ToString(), client.Status.ToString(), client.Notes, client.CreatedAt,
            subs.Select(Mappers.ToDto).ToList(),
            interactions.Select(Mappers.ToDto).ToList(),
            tickets.Select(t => t.ToDto()).ToList(),
            followUps.Select(Mappers.ToDto).ToList()));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateClientRequest request)
    {
        if (!Enum.TryParse<ClientType>(request.Type, true, out var type) || !ValidTypes.Contains(type))
            return BadRequest($"Unknown client type '{request.Type}'. Allowed: {string.Join(", ", ValidTypes)}.");
        if (!Enum.TryParse<ClientStatus>(request.Status, true, out var status) || !ValidStatuses.Contains(status))
            return BadRequest($"Unknown status '{request.Status}'. Allowed: {string.Join(", ", ValidStatuses)}.");

        var client = new Client
        {
            Name = request.Name.Trim(),
            ContactPerson = request.ContactPerson.Trim(),
            Phone = request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Address = request.Address,
            City = request.City,
            Type = type,
            Status = status,
            Notes = request.Notes,
            CreatedById = User.GetUserId()
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        // agenda entry so the first contact is scheduled right away
        if (request.FirstContactAt is { } when)
        {
            db.FollowUps.Add(new FollowUp
            {
                ClientId = client.Id,
                Title = $"First contact - {client.Name}",
                Description = "Initial outreach scheduled at client creation.",
                ScheduledAt = when,
                AssignedToId = User.GetUserId(),
                CreatedById = User.GetUserId()
            });
            await db.SaveChangesAsync();
        }

        return CreatedAtAction(nameof(GetById), new { id = client.Id }, new { client.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateClientRequest request)
    {
        var client = await db.Clients.FindAsync(id);
        if (client is null) return NotFound();

        if (!Enum.TryParse<ClientType>(request.Type, true, out var type) || !ValidTypes.Contains(type))
            return BadRequest($"Unknown client type '{request.Type}'.");
        if (!Enum.TryParse<ClientStatus>(request.Status, true, out var status) || !ValidStatuses.Contains(status))
            return BadRequest($"Unknown status '{request.Status}'.");

        client.Name = request.Name.Trim();
        client.ContactPerson = request.ContactPerson.Trim();
        client.Phone = request.Phone.Trim();
        client.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        client.Address = request.Address;
        client.City = request.City;
        client.Type = type;
        client.Status = status;
        client.Notes = request.Notes;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Pipeline quick-move (e.g. Potential -> Interested after a call).</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> ChangeStatus(int id, ClientStatusRequest request)
    {
        var client = await db.Clients.FindAsync(id);
        if (client is null) return NotFound();
        if (!Enum.TryParse<ClientStatus>(request.Status, true, out var status) || !ValidStatuses.Contains(status))
            return BadRequest($"Unknown status '{request.Status}'.");

        client.Status = status;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var client = await db.Clients.FindAsync(id);
        if (client is null) return NotFound();
        db.Clients.Remove(client); // cascades subscriptions/interactions/tickets; messages are kept
        await db.SaveChangesAsync();
        return NoContent();
    }
}
