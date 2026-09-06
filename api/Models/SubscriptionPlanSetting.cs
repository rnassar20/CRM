using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

/// <summary>
/// Link table between a subscription plan and a settings item (ew_set row).
/// Keeping this separate means the Subscription/SubscriptionPlan tables stay untouched:
/// a "plan" simply grants/includes certain settings (modules, currencies, flags, ...).
/// </summary>
[Table("plan_settings")]
public class PlanSetting
{
    [Column("plan_id")]
    public int PlanId { get; set; }

    [ForeignKey(nameof(PlanId))]
    public SubscriptionPlan Plan { get; set; } = null!;

    [Column("page")]
    [MaxLength(5)]
    public string Page { get; set; } = null!;

    [Column("pscode")]
    [MaxLength(5)]
    public string Pscode { get; set; } = null!;
}
