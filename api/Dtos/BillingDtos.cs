using System.ComponentModel.DataAnnotations;

namespace Crm.Api.Dtos;

public record PlanDto(int Id, string Name, string Cycle, decimal Price, bool IsActive);

public record SavePlanRequest(
    [Required, MaxLength(150)] string Name,
    [Required] string Cycle,
    [Range(0, 10_000_000)] decimal Price,
    bool IsActive = true);

public record SubscriptionDto(
    int Id, int ClientId, string ClientName, string ClientPhone,
    int PlanId, string PlanName, string Cycle,
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

/// <summary>One row in a client's payment history (a paid subscription period).</summary>
public record PaymentDto(
    int SubscriptionId, string PlanName, string Cycle,
    DateTime StartDate, DateTime ExpiryDate, decimal Amount,
    string? PaymentMethod, DateTime PaidAt, string? LicenseKey);
