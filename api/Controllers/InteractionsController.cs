using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/interactions")]
[Authorize]
public class InteractionsController(AppDbContext db) : ControllerBase
{
    private static readonly HashSet<InteractionType> ValidTypes = [InteractionType.Call, InteractionType.WhatsApp, InteractionType.Email, InteractionType.Visit, InteractionType.Sms];
    private static readonly HashSet<ClientStatus> AutoStatuses = [ClientStatus.Contacted, ClientStatus.Interested, ClientStatus.NotInterested, ClientStatus.Subscribed];

    /// <summary>Call/request log per client.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<InteractionDto>>> GetAll(
        [FromQuery] int? clientId,
        [FromQuery] int? userId,
        [FromQuery] string? type,
        [FromQuery] string? outcome,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Interactions.AsNoTracking().AsQueryable();
        if (clientId is { } cid) query = query.Where(i => i.ClientId == cid);
        if (userId is { } uid) query = query.Where(i => i.UserId == uid);
        if (Enum.TryParse<InteractionType>(type, true, out var it)) query = query.Where(i => i.Type == it);
        if (Enum.TryParse<InteractionOutcome>(outcome, true, out var io)) query = query.Where(i => i.Outcome == io);

        var total = await query.CountAsync();
        var items = await query
            .Include(i => i.Client).Include(i => i.User)
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<InteractionDto>(items.Select(Mappers.ToDto).ToList(), total, page, pageSize));
    }

    /// <summary>
    /// Records a call/WhatsApp/email/visit result. If NextFollowUpAt is provided an agenda
    /// entry is created automatically ("he's interested - call him again on ...").
    /// Optionally moves the client's pipeline status.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InteractionCreatedResponse>> Create(CreateInteractionRequest request)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId);
        if (client is null) return BadRequest($"Client {request.ClientId} not found.");

        if (!Enum.TryParse<InteractionType>(request.Type, true, out var type) || !ValidTypes.Contains(type))
            return BadRequest($"Unknown interaction type '{request.Type}'. Allowed: {string.Join(", ", ValidTypes)}.");
        if (!Enum.TryParse<InteractionOutcome>(request.Outcome, true, out var outcome))
            return BadRequest($"Unknown outcome '{request.Outcome}'.");

        ClientStatus? newStatus = null;
        if (request.NewClientStatus is not null)
        {
            if (!Enum.TryParse<ClientStatus>(request.NewClientStatus, true, out var parsed) || !AutoStatuses.Contains(parsed))
                return BadRequest($"Unknown client status '{request.NewClientStatus}'.");
            newStatus = parsed;
        }

        var interaction = new Interaction
        {
            ClientId = client.Id,
            Type = type,
            Outcome = outcome,
            Notes = request.Notes,
            NextFollowUpAt = request.NextFollowUpAt,
            UserId = User.GetUserId()
        };
        db.Interactions.Add(interaction);

        FollowUp? followUp = null;
        if (request.NextFollowUpAt is { } when)
        {
            followUp = new FollowUp
            {
                ClientId = client.Id,
                Title = $"Follow-up ({type}): {client.Name}",
                Description = $"Created from {type} log with outcome '{outcome}'.",
                ScheduledAt = when,
                AssignedToId = User.GetUserId(),
                CreatedById = User.GetUserId(),
                SourceInteraction = interaction
            };
            db.FollowUps.Add(followUp);
        }

        if (newStatus is { } ns)
        {
            client.Status = ns;
        }
        else if (outcome is InteractionOutcome.DealClosed)
        {
            client.Status = ClientStatus.Subscribed;
        }
        else if (client.Status is ClientStatus.Potential or ClientStatus.Contacted)
        {
            client.Status = outcome switch
            {
                InteractionOutcome.Interested or InteractionOutcome.CallbackRequested => ClientStatus.Interested,
                InteractionOutcome.NotInterested => ClientStatus.NotInterested,
                _ => ClientStatus.Contacted
            };
        }

        await db.SaveChangesAsync();
        interaction.Client = client;
        interaction.User = await db.Users.FirstAsync(u => u.Id == interaction.UserId);

        return Ok(new InteractionCreatedResponse(interaction.ToDto(), followUp?.Id));
    }
}

public record InteractionCreatedResponse(InteractionDto Interaction, int? FollowUpId);
