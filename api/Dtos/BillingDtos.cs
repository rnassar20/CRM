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
    [Range(1, int.MaxValue)] int ClientId,
    [Range(1, int.MaxValue)] int PlanId,
    DateTime? StartDate,
    [Range(0, 10_000_000)] decimal? Price,
    [MaxLength(1000)] string? Notes);

public record MarkPaidRequest([MaxLength(50)] string? PaymentMethod);

/// <summary>One row in a client's payment history (a paid subscription period).</summary>
public record PaymentDto(
    int SubscriptionId, string PlanName, string Cycle,
    DateTime StartDate, DateTime ExpiryDate, decimal Amount,
    string? PaymentMethod, DateTime PaidAt, string? LicenseKey);
