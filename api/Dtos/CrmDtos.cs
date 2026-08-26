using System.ComponentModel.DataAnnotations;

namespace Crm.Api.Dtos;

public record InteractionDto(
    int Id, int ClientId, string ClientName, string Type, string Outcome,
    string? Notes, DateTime? NextFollowUpAt, int UserId, string UserName, DateTime CreatedAt);

public record CreateInteractionRequest(
    [Range(1, int.MaxValue)] int ClientId,
    [Required, MaxLength(25)] string Outcome,
    [MaxLength(2000)] string? Notes,
    /// <summary>If set (e.g. client asked to call back), an agenda follow-up is auto-created.</summary>
    DateTime? NextFollowUpAt,
    /// <summary>Optionally move the client pipeline status as a result of this interaction.</summary>
    [MaxLength(30)] string? NewClientStatus,
    string Type = "Call");

public record FollowUpDto(
    int Id, int ClientId, string ClientName, string Title, string? Description,
    string Type, int? TicketId, string? TicketTitle,
    DateTime ScheduledAt, string Status, int AssignedToId, string AssignedToName,
    DateTime? ReminderSentAt, DateTime CreatedAt);

public record CreateFollowUpRequest(
    [Range(1, int.MaxValue)] int ClientId,
    [Required, MaxLength(200)] string Title,
    [MaxLength(2000)] string? Description,
    [Required] DateTime ScheduledAt,
    /// <summary>Marketing (default), Internal (build/version work) or Support (linked to a ticket).</summary>
    string Type = "Marketing",
    int? TicketId = null,
    int? AssignedToId = null);

public record UpdateFollowUpRequest(
    [MaxLength(200)] string? Title,
    [MaxLength(2000)] string? Description,
    DateTime? ScheduledAt,
    [MaxLength(15)] string? Status,
    [MaxLength(12)] string? Type,
    int? TicketId,
    int? AssignedToId);
