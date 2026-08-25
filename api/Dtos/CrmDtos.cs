using System.ComponentModel.DataAnnotations;

namespace Crm.Api.Dtos;

public record InteractionDto(
    int Id, int ClientId, string ClientName, string Type, string Outcome,
    string? Notes, DateTime? NextFollowUpAt, int UserId, string UserName, DateTime CreatedAt);

public record CreateInteractionRequest(
    [Required] int ClientId,
    [Required] string Outcome,
    string? Notes,
    /// <summary>If set (e.g. client asked to call back), an agenda follow-up is auto-created.</summary>
    DateTime? NextFollowUpAt,
    /// <summary>Optionally move the client pipeline status as a result of this interaction.</summary>
    string? NewClientStatus,
    string Type = "Call");

public record FollowUpDto(
    int Id, int ClientId, string ClientName, string Title, string? Description,
    string Type, int? TicketId, string? TicketTitle,
    DateTime ScheduledAt, string Status, int AssignedToId, string AssignedToName,
    DateTime? ReminderSentAt, DateTime CreatedAt);

public record CreateFollowUpRequest(
    [Required] int ClientId,
    [Required, MaxLength(200)] string Title,
    string? Description,
    [Required] DateTime ScheduledAt,
    /// <summary>Marketing (default), Internal (build/version work) or Support (linked to a ticket).</summary>
    string Type = "Marketing",
    int? TicketId = null,
    int? AssignedToId = null);

public record UpdateFollowUpRequest(
    string? Title,
    string? Description,
    DateTime? ScheduledAt,
    string? Status,
    string? Type,
    int? TicketId,
    int? AssignedToId);
