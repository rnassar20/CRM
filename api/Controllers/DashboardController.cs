using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> GetStats()
    {
        var today = DateTime.UtcNow.Date;
        var now = DateTime.Now;

        var clientsByStatus = await db.Persons
            .Where(p => p.PersonType == 12)
            .Include(p => p.CrmExtension)
            .GroupBy(p => p.CrmExtension != null ? p.CrmExtension.Status : "Unknown")
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var clientsByType = await db.Persons
            .Where(p => p.PersonType == 12)
            .Include(p => p.CrmExtension)
            .GroupBy(p => p.CrmExtension != null ? p.CrmExtension.ClientType : "Unknown")
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var ticketsByStatus = await db.Tickets
            .GroupBy(t => t.Status)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var subsActive = await db.Subscriptions.CountAsync(s => s.ExpiryDate >= today);
        var subsExpiring30 = await db.Subscriptions.CountAsync(s => s.ExpiryDate >= today && s.ExpiryDate < today.AddDays(30));
        var subsExpired = await db.Subscriptions.CountAsync(s => s.ExpiryDate < today);
        var subsUnpaidActive = await db.Subscriptions.CountAsync(s => s.ExpiryDate >= today && s.PaymentStatus == PaymentStatus.Unpaid);

        var upcoming = await db.FollowUps
            .Include(f => f.Client)
            .Include(f => f.AssignedTo)
            .Where(f => f.Status == FollowUpStatus.Pending && f.ScheduledAt >= now && f.ScheduledAt <= now.AddDays(7))
            .OrderBy(f => f.ScheduledAt)
            .Take(10)
            .ToListAsync();

        var recent = await db.Interactions
            .Include(i => i.Client)
            .Include(i => i.User)
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .ToListAsync();

        var waSent = await db.WhatsAppMessages
            .CountAsync(m => m.Direction == WhatsAppDirection.Outgoing && m.Status == WhatsAppStatus.Sent && m.CreatedAt >= today.AddDays(-30));

        var clientsTotal = await db.Persons.CountAsync(p => p.PersonType == 12);

        return Ok(new DashboardStatsDto(
            clientsTotal,
            clientsByStatus,
            clientsByType,
            subsActive,
            subsExpiring30,
            subsExpired,
            subsUnpaidActive,
            ticketsByStatus,
            ticketsByStatus.GetValueOrDefault(TicketStatus.Open.ToString()) + ticketsByStatus.GetValueOrDefault(TicketStatus.InProgress.ToString()),
            await db.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Pending && f.ScheduledAt.Date == today),
            await db.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Pending && f.ScheduledAt < now),
            waSent,
            upcoming.Select(f => f.ToDto()).ToList(),
            recent.Select(i => i.ToDto()).ToList()));
    }
}
