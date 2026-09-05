using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("crm_client_extension")]
public class CrmClientExtension
{
    [Key, Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("CrmExtension")]
    public Person Person { get; set; } = null!;

    /// <summary>Potential / Contacted / Interested / NotInterested / Subscribed</summary>
    [Column("status")]
    [MaxLength(30)]
    public string Status { get; set; } = "Potential";

    /// <summary>Pharmacy / GiftShop / DoctorClinic / Hospital / Other</summary>
    [Column("client_type")]
    [MaxLength(30)]
    public string ClientType { get; set; } = "Other";

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
