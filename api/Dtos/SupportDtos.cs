using System.ComponentModel.DataAnnotations;

namespace Crm.Api.Dtos;

public record TicketDto(
    int Id, int ClientId, string ClientName, string Title, string? Description,
    string Priority, string Status, int? AssignedToId, string? AssignedToName,
    string CreatedByName, DateTime CreatedAt, DateTime UpdatedAt, DateTime? ResolvedAt, int CommentCount);

public record TicketCommentDto(int Id, int UserId, string UserName, string Body, bool IsInternal, DateTime CreatedAt);

public record CreateTicketRequest(
    [Required] int ClientId,
    [Required, MaxLength(200)] string Title,
    string? Description,
    string Priority = "Medium",
    int? AssignedToId = null);

public record UpdateTicketRequest(
    string? Title,
    string? Description,
    string? Priority,
    string? Status,
    int? AssignedToId,
    bool Unassign = false);

public record AddCommentRequest([Required] string Body, bool IsInternal = false);
