using Crm.Api.Data;
using Crm.Api.Models;
using Crm.Api.Services.WhatsApp;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Services;

/// <summary>
/// Hangfire replacement for the old <c>ReminderWorker</c> BackgroundService.
/// The polling loop is gone: two recurring jobs scan on a cron schedule, and each
/// WhatsApp message is sent by its own fire-and-forget job so Hangfire's automatic
/// retry handles transient Meta Cloud API failures.
/// </summary>
public class ReminderJobs(IServiceScopeFactory scopeFactory, ILogger<ReminderJobs> logger)
{
    private static readonly int[] ReminderDays = [30, 15, 7, 1];

    /// <summary>Recurring job: enqueues one send job per recipient that has not been notified yet.</summary>
    public async Task RunExpiryRemindersAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var expiryTemplate = cfg["WhatsApp:Templates:ExpiryReminder"]
            ?? "Hi {client}, your {plan} subscription expires on {expiry} (in {days} day(s)). Contact us to renew and keep your ERP running.";

        var today = DateTime.UtcNow.Date;

        var activeSubs = await db.Subscriptions
            .Include(s => s.Client).ThenInclude(c => c.Contacts)
            .Include(s => s.Plan)
            .Where(s => s.ExpiryDate >= today)
            .ToListAsync();

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

            foreach (var (phone, name) in Recipients(sub.Client))
            {
                // Skip only when a message is already planned (Queued) or delivered (Sent).
                // Failed rows are intentionally re-enqueued so the next cycle gives another shot.
                var planned = await db.WhatsAppMessages.AnyAsync(m =>
                    m.SubscriptionId == sub.Id && m.RelatedTag == tag && m.ToPhone == phone
                    && (m.Status == WhatsAppStatus.Queued || m.Status == WhatsAppStatus.Sent));
                if (planned) continue;

                var personalized = name is null ? body : body.Replace("{contact}", name);
                BackgroundJob.Enqueue<ReminderJobs>(j =>
                    j.SendWhatsAppAsync(sub.Id, phone, personalized, sub.ClientId, tag));
            }
        }
    }

    /// <summary>Recurring job: flags agenda follow-ups as reminded 24h ahead, auto-marks stale ones missed.</summary>
    public async Task RunFollowUpProcessingAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.Now;

        var dueFollowUps = await db.FollowUps
            .Where(f => f.Status == FollowUpStatus.Pending && f.ReminderSentAt == null && f.ScheduledAt <= now.AddHours(24))
            .ToListAsync();
        foreach (var fu in dueFollowUps)
            fu.ReminderSentAt = DateTime.UtcNow;

        var stale = await db.FollowUps
            .Where(f => f.Status == FollowUpStatus.Pending && f.ScheduledAt < now.AddDays(-2))
            .ToListAsync();
        foreach (var fu in stale)
            fu.Status = FollowUpStatus.Missed;

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Fire-and-forget send job (enqueued by the expiry reminder scan and by the license endpoints).
    /// Idempotent per (subscription, tag, phone): retries update the existing row instead of duplicating.
    /// Transient failures throw so Hangfire's <see cref="AutomaticRetryAttribute"/> retries with backoff
    /// (after the attempts run out the job is left in the dashboard as Failed); permanent failures are
    /// recorded and skipped, because retrying them would just fail again.
    /// </summary>
    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task SendWhatsAppAsync(int subscriptionId, string phone, string body, int clientId, string tag)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IWhatsAppSender>();

        var msg = await db.WhatsAppMessages
            .Where(m => m.SubscriptionId == subscriptionId && m.RelatedTag == tag && m.ToPhone == phone)
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync();

        if (msg is { Status: WhatsAppStatus.Sent }) return; // already delivered on an earlier attempt

        if (msg is null)
        {
            msg = new WhatsAppMessage
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
        }
        else
        {
            msg.Body = body;
            msg.Status = WhatsAppStatus.Queued;
            msg.Error = null;
        }
        await db.SaveChangesAsync();

        var result = await sender.SendAsync(phone, body);
        if (result.Ok)
        {
            msg.Status = WhatsAppStatus.Sent;
            msg.ProviderMessageId = result.ProviderMessageId;
            msg.SentAt = DateTime.UtcNow;
            msg.Error = null;
            await db.SaveChangesAsync();
            logger.LogInformation("WhatsApp sent to {Phone} (sub {SubId}, tag {Tag})", phone, subscriptionId, tag);
            return;
        }

        msg.Status = WhatsAppStatus.Failed;
        msg.Error = result.Error;
        await db.SaveChangesAsync();

        if (result.Failure == SendFailureKind.Permanent)
        {
            // Retrying a permanent failure just fails again (bad number, blocked, template error).
            // Record it and finish; Hangfire sees a completed job, the row carries the failure.
            logger.LogWarning("Permanent WhatsApp failure for {Phone} (sub {SubId}): {Error}", phone, subscriptionId, result.Error);
            return;
        }

        logger.LogWarning("Transient WhatsApp failure for {Phone} (sub {SubId}): {Error} - will be retried by Hangfire", phone, subscriptionId, result.Error);
        throw new InvalidOperationException($"WhatsApp delivery failed for {phone}: {result.Error}");
    }

    /// <summary>
    /// Synchronous send path used by HTTP controllers (license delivery / resend), which need the
    /// outcome in the response. Logs the message row, sends immediately, and records Sent/Failed.
    /// Retry for the scheduled reminder path lives in <see cref="SendWhatsAppAsync"/>.
    /// </summary>
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
}
