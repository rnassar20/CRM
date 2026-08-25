namespace Crm.Api.Models;

/// <summary>A recorded touchpoint with a client: phone call, WhatsApp msg, email, visit...</summary>
public class Interaction
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public InteractionType Type { get; set; } = InteractionType.Call;
    public InteractionOutcome Outcome { get; set; }
    public string? Notes { get; set; }
    /// <summary>If the outcome requires calling back, this holds the agreed next contact time.</summary>
    public DateTime? NextFollowUpAt { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An agenda entry: planned contact / visit / task for a client.</summary>
public class FollowUp
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public FollowUpType Type { get; set; } = FollowUpType.Marketing;
    /// <summary>Support follow-ups reference the ticket being chased (shows its number/title).</summary>
    public int? TicketId { get; set; }
    public Ticket? Ticket { get; set; }
    public DateTime ScheduledAt { get; set; }
    public FollowUpStatus Status { get; set; } = FollowUpStatus.Pending;
    public int AssignedToId { get; set; }
    public User AssignedTo { get; set; } = null!;
    public int CreatedById { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    public int? SourceInteractionId { get; set; }
    public Interaction? SourceInteraction { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
