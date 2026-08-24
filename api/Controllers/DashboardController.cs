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

        var clientsByStatus = await db.Clients
            .GroupBy(c => c.Status)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var clientsByType = await db.Clients
            .GroupBy(c => c.Type)
            .Select(g => new { Key = g.Key.ToString(), Count = g.Count() })
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

        return Ok(new DashboardStatsDto(
            ClientsTotal: await db.Clients.CountAsync(),
            ClientsByStatus: clientsByStatus,
            ClientsByType: clientsByType,
            SubscriptionsActive: subsActive,
            SubscriptionsExpiringIn30: subsExpiring30,
            SubscriptionsExpired: subsExpired,
            SubscriptionsUnpaidActive: subsUnpaidActive,
            TicketsByStatus: ticketsByStatus,
            TicketsOpen: ticketsByStatus.GetValueOrDefault(TicketStatus.Open.ToString()) + ticketsByStatus.GetValueOrDefault(TicketStatus.InProgress.ToString()),
            FollowUpsToday: await db.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Pending && f.ScheduledAt.Date == today),
            FollowUpsOverdue: await db.FollowUps.CountAsync(f => f.Status == FollowUpStatus.Pending && f.ScheduledAt < now),
            WhatsAppSentLast30Days: waSent,
            UpcomingFollowUps: upcoming.Select(f => f.ToDto()).ToList(),
            RecentInteractions: recent.Select(i => i.ToDto()).ToList()));
    }
}
