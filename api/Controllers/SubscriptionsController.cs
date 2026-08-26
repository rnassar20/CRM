using Crm.Api.Data;
using Crm.Api.Dtos;
using Crm.Api.Models;
using Crm.Api.Services;
using Crm.Api.Services.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController(AppDbContext db, ILicenseKeyService license, IWhatsAppSender sender, IConfiguration config) : ControllerBase
{
    private static readonly HashSet<PaymentStatus> ValidPaymentStatuses = [PaymentStatus.Unpaid, PaymentStatus.Paid];

    [HttpGet]
    public async Task<ActionResult<PagedResult<SubscriptionDto>>> GetAll(
        [FromQuery] int? clientId,
        [FromQuery] string? paymentStatus,
        [FromQuery] int? expiringInDays,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var today = DateTime.UtcNow.Date;
        var query = db.Subscriptions.AsNoTracking().AsQueryable();

        if (clientId is { } cid) query = query.Where(s => s.ClientId == cid);
        if (Enum.TryParse<PaymentStatus>(paymentStatus, true, out var ps) && ValidPaymentStatuses.Contains(ps))
            query = query.Where(s => s.PaymentStatus == ps);
        if (expiringInDays is { } days)
            query = query.Where(s => s.ExpiryDate >= today && s.ExpiryDate < today.AddDays(days));

        var total = await query.CountAsync();

        var ordered = expiringInDays == null
            ? query.Include(s => s.Client).Include(s => s.Plan).OrderByDescending(s => s.CreatedAt)
            : query.Include(s => s.Client).Include(s => s.Plan).OrderBy(s => s.ExpiryDate);

        var subs = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<SubscriptionDto>(subs.Select(Mappers.ToDto).ToList(), total, page, pageSize));
    }

    [HttpPost]
    public async Task<ActionResult<SubscriptionDto>> Create(CreateSubscriptionRequest request)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId);
        if (client is null) return BadRequest($"Client {request.ClientId} not found.");
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == request.PlanId && p.IsActive);
        if (plan is null) return BadRequest($"Active plan {request.PlanId} not found.");

        // renewals stack on top of the current expiry; brand-new subs start today
        var start = request.StartDate?.Date ?? DateTime.UtcNow.Date;
        if (request.StartDate is null)
        {
            var latestExpiry = await db.Subscriptions
                .Where(s => s.ClientId == client.Id)
                .OrderByDescending(s => s.ExpiryDate)
                .Select(s => (DateTime?)s.ExpiryDate)
                .FirstOrDefaultAsync();
            if (latestExpiry is { } le && le.Date > start) start = le.Date;
        }

        var sub = new Subscription
        {
            ClientId = client.Id,
            PlanId = plan.Id,
            Cycle = plan.Cycle,
            StartDate = start,
            ExpiryDate = start.AddCycle(plan.Cycle),
            Price = request.Price ?? plan.Price,
            Notes = request.Notes,
            PaymentStatus = PaymentStatus.Unpaid
        };
        db.Subscriptions.Add(sub);

        if (client.Status != ClientStatus.Subscribed)
            client.Status = ClientStatus.Subscribed;

        await db.SaveChangesAsync();
        sub.Client = client;
        sub.Plan = plan;
        return CreatedAtAction(nameof(GetById), new { id = sub.Id }, sub.ToDto());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SubscriptionDto>> GetById(int id)
    {
        var sub = await db.Subscriptions.AsNoTracking()
            .Include(s => s.Client).Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id);
        return sub is null ? NotFound() : Ok(sub.ToDto());
    }

    /// <summary>
    /// Marks payment as received: generates the encrypted activation key and delivers it via WhatsApp.
    /// The key is returned once here for manual fallback.
    /// </summary>
    [HttpPost("{id:int}/mark-paid")]
    public async Task<ActionResult<object>> MarkPaid(int id, MarkPaidRequest request)
    {
        var sub = await db.Subscriptions
            .Include(s => s.Client).Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sub is null) return NotFound();
        if (sub.PaymentStatus == PaymentStatus.Paid) return BadRequest("Subscription is already marked as paid.");

        sub.PaymentStatus = PaymentStatus.Paid;
        sub.PaymentMethod = string.IsNullOrWhiteSpace(request?.PaymentMethod) ? null : request.PaymentMethod.Trim();
        sub.PaidAt = DateTime.UtcNow;

        var key = sub.LicenseKey ?? license.GenerateKey(sub.ClientId, sub.Id, sub.ExpiryDate);
        if (sub.LicenseKey is null)
        {
            sub.LicenseKey = key;
            sub.LicenseKeyHash = license.HashKey(key);
            sub.LicenseKeyIssuedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();

        var template = config["WhatsApp:Templates:LicenseDelivered"]
            ?? "Thank you {client}! Your payment for {plan} is confirmed. Activation key:\n{key}\nOpen your ERP > Help > Activate Subscription and enter this key. Licensed until {expiry}.";
        var body = template
            .Replace("{client}", sub.Client.Name)
            .Replace("{plan}", sub.Plan.Name)
            .Replace("{key}", key)
            .Replace("{expiry}", sub.ExpiryDate.ToString("yyyy-MM-dd"));

        var (sent, status) = await SendToRecipientsAsync(sub, body, $"license-{sub.Id}", HttpContext.RequestAborted);

        return Ok(new { subscription = sub.ToDto(), whatsappStatus = status, licenseKey = key });
    }

    /// <summary>Re-delivers the existing activation key (same key, no regeneration).</summary>
    [HttpPost("{id:int}/resend-key")]
    public async Task<ActionResult<object>> ResendKey(int id)
    {
        var sub = await db.Subscriptions
            .Include(s => s.Client).ThenInclude(c => c.Contacts).Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sub is null) return NotFound();
        if (sub.LicenseKey is null) return BadRequest("No key issued yet. Mark the subscription as paid first.");

        var body = $"Hi {{contact}}, your ERP activation key:\n{sub.LicenseKey}\nValid until {sub.ExpiryDate:yyyy-MM-dd}.";
        var (_, status) = await SendToRecipientsAsync(sub, body, $"license-resend-{Guid.NewGuid():N}", HttpContext.RequestAborted);

        return Ok(new { sent = status != "Skipped", status });
    }

    /// <summary>Sends a pre-formatted body to the primary contact plus every opted-in client contact.</summary>
    private async Task<(int Sent, string Status)> SendToRecipientsAsync(Subscription sub, string bodyTemplate, string tag, CancellationToken ct)
    {
        var recipients = ReminderWorker.Recipients(sub.Client).ToList();
        if (recipients.Count == 0) return (0, "Skipped");

        var sent = 0;
        WhatsAppStatus last = WhatsAppStatus.Failed;
        foreach (var (phone, name) in recipients)
        {
            var body = name is null ? bodyTemplate : bodyTemplate.Replace("{contact}", name);
            var msg = await ReminderWorker.QueueAndSendAsync(db, sender, phone, body, sub.ClientId, sub.Id, tag, ct);
            if (msg.Status == WhatsAppStatus.Sent) sent++;
            last = msg.Status;
        }
        var who = recipients.Count == 1 ? "1 recipient" : $"{recipients.Count} recipients";
        return (sent, sent == 0 ? $"Failed ({who})" : $"Sent to {sent}/{who} ({last})");
    }

    /// <summary>Offline validation endpoint - mirrors what the desktop ERP does with a entered key.</summary>
    [HttpPost("validate-key")]
    [AllowAnonymous]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("validate")]
    public async Task<ActionResult<LicenseCheckResponse>> ValidateKey([FromBody] ValidateKeyRequest request)
    {
        // Note: failure reasons are deliberately generic so internal crypto details are never
        // exposed to callers. The desktop ERP only needs a yes/no + the parsed facts.
        try
        {
            var (clientId, subId, expiry) = license.ParseKey(request.Key);
            var hashMatches = await db.Subscriptions.AnyAsync(s =>
                s.Id == subId && s.LicenseKeyHash == license.HashKey(request.Key));
            return Ok(new LicenseCheckResponse(true, null, clientId, subId, expiry, hashMatches));
        }
        catch (Exception)
        {
            return Ok(new LicenseCheckResponse(false, "Invalid license key.", null, null, null, false));
        }
    }
}

public record ValidateKeyRequest(string Key);

public static class SubscriptionExtensions
{
    /// <summary>Calendar-accurate period end: monthly = +1 month, yearly = +1 year.</summary>
    public static DateTime AddCycle(this DateTime start, BillingCycle cycle) =>
        cycle == BillingCycle.Yearly ? start.AddYears(1) : start.AddMonths(1);
}
