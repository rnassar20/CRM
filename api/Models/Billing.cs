using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("SubscriptionPlans")]
public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public BillingCycle Cycle { get; set; } = BillingCycle.Yearly;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

[Table("Subscriptions")]
public class Subscription
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    [ForeignKey("ClientId")]
    public Client Client { get; set; } = null!;
    public int PlanId { get; set; }
    [ForeignKey("PlanId")]
    public SubscriptionPlan Plan { get; set; } = null!;
    public BillingCycle Cycle { get; set; } = BillingCycle.Yearly;
    public DateTime StartDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal Price { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? LicenseKey { get; set; }
    public string? LicenseKeyHash { get; set; }
    public DateTime? LicenseKeyIssuedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
