using System.ComponentModel.DataAnnotations;

namespace Crm.Api.Dtos;

public record PlanDto(int Id, string Name, int DurationDays, decimal Price, bool IsActive);

public record SavePlanRequest(
    [Required, MaxLength(150)] string Name,
    [Range(1, 3650)] int DurationDays,
    [Range(0, 10_000_000)] decimal Price,
    bool IsActive = true);

public record SubscriptionDto(
    int Id, int ClientId, string ClientName, string ClientPhone,
    int PlanId, string PlanName,
    DateTime StartDate, DateTime ExpiryDate, decimal Price,
    string PaymentStatus, string? PaymentMethod, DateTime? PaidAt,
    string? LicenseKey, DateTime? LicenseKeyIssuedAt, string? Notes, DateTime CreatedAt);

public record CreateSubscriptionRequest(
    [Required] int ClientId,
    [Required] int PlanId,
    DateTime? StartDate,
    decimal? Price,
    string? Notes);

public record MarkPaidRequest(string? PaymentMethod);
