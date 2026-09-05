using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_insurance")]
public class PersonInsurance
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Insurances")]
    public Person Person { get; set; } = null!;

    [Column("insurance_type")]
    public short InsuranceType { get; set; }

    [Column("seq")]
    public short Seq { get; set; }

    [Column("effective_date")]
    public DateTime EffectiveDate { get; set; }

    [Column("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [Column("org_id")]
    public int? OrgId { get; set; }

    [Column("ben_org_id")]
    [MaxLength(50)]
    public string? BenOrgId { get; set; }

    [Column("covered")]
    public decimal? Covered { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
