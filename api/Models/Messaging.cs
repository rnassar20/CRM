namespace Crm.Api.Models;

/// <summary>
/// Every outbound/inbound WhatsApp message is logged here so the schema is ready
/// for text, voice notes and images once the real gateway is connected.
/// </summary>
public class WhatsAppMessage
{
    public int Id { get; set; }
    public string ToPhone { get; set; } = "";
    public string Body { get; set; } = "";
    public WhatsAppMediaType MediaType { get; set; } = WhatsAppMediaType.Text;
    public string? MediaUrl { get; set; }
    public WhatsAppDirection Direction { get; set; } = WhatsAppDirection.Outgoing;
    public WhatsAppStatus Status { get; set; } = WhatsAppStatus.Queued;
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public int? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    /// <summary>Dedup/business tag, e.g. "expiry-30", "license-paid".</summary>
    public string? RelatedTag { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
}
