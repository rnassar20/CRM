using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_clinical_detail")]
public class PersonClinicalDetail
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("ClinicalDetails")]
    public Person? Person { get; set; }

    [Column("seq")]
    public short Seq { get; set; }

    [Column("clinical_id")]
    public int ClinicalId { get; set; }

    [Column("clinical_type")]
    public short? ClinicalType { get; set; }

    [Column("value")]
    [MaxLength(20)]
    public string? Value { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
