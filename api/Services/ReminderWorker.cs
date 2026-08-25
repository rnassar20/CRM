using Crm.Api.Data;
using Crm.Api.Models;
using Crm.Api.Services.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Services;

/// <summary>
/// Periodic job:
///  - sends expiry reminders via WhatsApp at 30/15/7/1 days before subscription expiry (deduped by tag),
///  - flags agenda follow-ups as reminded 24h before their time, and auto-marks stale ones missed.
/// </summary>
public class ReminderWorker(IServiceScopeFactory scopeFactory, ILogger<ReminderWorker> logger) : BackgroundService
{
    private static readonly int[] ReminderDays = [30, 15, 7, 1];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // small delay so startup/db seeding finishes first
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch (TaskCanceledException) { }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "ReminderWorker cycle failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
            catch (TaskCanceledException) { break; }
        }
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IWhatsAppSender>();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var expiryTemplate = cfg["WhatsApp:Templates:ExpiryReminder"]
            ?? "Hi {client}, your {plan} subscription expires on {expiry} (in {days} day(s)). Contact us to renew and keep your ERP running.";

        var today = DateTime.UtcNow.Date;

        // ---- subscription expiry reminders ----
        var activeSubs = await db.Subscriptions
            .Include(s => s.Client).ThenInclude(c => c.Contacts)
            .Include(s => s.Plan)
            .Where(s => s.ExpiryDate >= today)
            .ToListAsync(ct);

        foreach (var sub in activeSubs)
        {
            var daysLeft = (sub.ExpiryDate.Date - today).Days;
            if (!ReminderDays.Contains(daysLeft)) continue;

            var tag = $"expiry-{daysLeft}";
            var body = expiryTemplate
                .Replace("{client}", sub.Client.Name)
                .Replace("{plan}", sub.Plan.Name)
                .Replace("{expiry}", sub.ExpiryDate.ToString("yyyy-MM-dd"))
                .Replace("{days}", daysLeft.ToString());

            // primary contact + every opted-in secondary contact; dedupe per recipient
            foreach (var (phone, name) in Recipients(sub.Client))
            {
                var already = await db.WhatsAppMessages.AnyAsync(
                    m => m.SubscriptionId == sub.Id && m.RelatedTag == tag && m.ToPhone == phone, ct);
                if (already) continue;

                var personalized = name is null ? body : body.Replace("{contact}", name);
                await QueueAndSendAsync(db, sender, phone, personalized, sub.ClientId, sub.Id, tag, ct);
            }
        }

        // ---- follow-up reminders (internal flag; visible on dashboard/agenda) ----
        var now = DateTime.Now;
        var dueFollowUps = await db.FollowUps
            .Where(f => f.Status == FollowUpStatus.Pending && f.ReminderSentAt == null && f.ScheduledAt <= now.AddHours(24))
            .ToListAsync(ct);
        foreach (var fu in dueFollowUps)
            fu.ReminderSentAt = DateTime.UtcNow;

        // ---- auto-miss long overdue follow-ups ----
        var stale = await db.FollowUps
            .Where(f => f.Status == FollowUpStatus.Pending && f.ScheduledAt < now.AddDays(-2))
            .ToListAsync(ct);
        foreach (var fu in stale)
            fu.Status = FollowUpStatus.Missed;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Everyone who should receive a client's WhatsApp notifications: the primary contact plus
    /// every secondary contact flagged AllowWhatsApp. Deduplicated by phone number.
    /// </summary>
    public static IEnumerable<(string Phone, string? Name)> Recipients(Client client)
    {
        if (!string.IsNullOrWhiteSpace(client.Phone))
            yield return (client.Phone.Trim(), null);

        foreach (var c in client.Contacts)
        {
            if (c.AllowWhatsApp && !string.IsNullOrWhiteSpace(c.Phone))
                yield return (c.Phone.Trim(), c.Name);
        }
    }

    /// <summary>Queues a WhatsApp message row then sends it through the configured provider. Shared by workers and controllers.</summary>
    public static async Task<WhatsAppMessage> QueueAndSendAsync(AppDbContext db, IWhatsAppSender sender,
        string phone, string body, int? clientId, int? subscriptionId, string? tag, CancellationToken ct)
    {
        var msg = new WhatsAppMessage
        {
            ToPhone = phone,
            Body = body,
            Direction = WhatsAppDirection.Outgoing,
            Status = WhatsAppStatus.Queued,
            ClientId = clientId,
            SubscriptionId = subscriptionId,
            RelatedTag = tag
        };
        db.WhatsAppMessages.Add(msg);
        await db.SaveChangesAsync(ct);

        var result = await sender.SendAsync(phone, body, ct);
        msg.Status = result.Ok ? WhatsAppStatus.Sent : WhatsAppStatus.Failed;
        msg.ProviderMessageId = result.ProviderMessageId;
        msg.Error = result.Error;
        msg.SentAt = result.Ok ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(ct);
        return msg;
    }
}
