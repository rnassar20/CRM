namespace Crm.Api.Models;

public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Billing period: a Monthly plan lasts 1 month from start, a Yearly plan 12 months.</summary>
    public BillingCycle Cycle { get; set; } = BillingCycle.Yearly;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Subscription
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public int PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;
    /// <summary>Cycle snapshot from the plan at purchase time.</summary>
    public BillingCycle Cycle { get; set; } = BillingCycle.Yearly;
    public DateTime StartDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public decimal Price { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>The activation key handed to the customer (enterable in the desktop ERP).</summary>
    public string? LicenseKey { get; set; }
    /// <summary>SHA-256 of the normalized key, used for tamper checks.</summary>
    public string? LicenseKeyHash { get; set; }
    public DateTime? LicenseKeyIssuedAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
