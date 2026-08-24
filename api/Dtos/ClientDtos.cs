using System.ComponentModel.DataAnnotations;
using Crm.Api.Models;

namespace Crm.Api.Dtos;

public record ClientListItemDto(
    int Id, string Name, string ContactPerson, string Phone, string? Email,
    string? City, string Type, string Status,
    int? SubscriptionId, string? PlanName, DateTime? ExpiryDate, string? PaymentStatus);

public record ClientDetailDto(
    int Id, string Name, string ContactPerson, string Phone, string? Email,
    string? Address, string? City, string Type, string Status, string? Notes,
    DateTime CreatedAt,
    IReadOnlyList<SubscriptionDto> Subscriptions,
    IReadOnlyList<InteractionDto> Interactions,
    IReadOnlyList<TicketDto> Tickets,
    IReadOnlyList<FollowUpDto> FollowUps);

public record CreateClientRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(200)] string ContactPerson,
    [Required, Phone] string Phone,
    [EmailAddress] string? Email,
    string? Address,
    [MaxLength(100)] string? City,
    [Required] string Type,
    [Required] string Status,
    string? Notes,
    /// <summary>Optional agenda entry created with the client ("when should I contact them first").</summary>
    DateTime? FirstContactAt);

public record UpdateClientRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(200)] string ContactPerson,
    [Required, Phone] string Phone,
    [EmailAddress] string? Email,
    string? Address,
    [MaxLength(100)] string? City,
    [Required] string Type,
    [Required] string Status,
    string? Notes);

public record ClientStatusRequest([Required] string Status);
