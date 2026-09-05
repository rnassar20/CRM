using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_symptoms")]
public class PersonSymptoms
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Symptoms")]
    public Person Person { get; set; } = null!;

    [Column("symptom_seq")]
    public short SymptomSeq { get; set; }

    [Column("effective_date")]
    public DateTime EffectiveDate { get; set; }

    [Column("symptom_id")]
    public int? SymptomId { get; set; }

    [Column("remarks")]
    [MaxLength(255)]
    public string? Remarks { get; set; }

    [Column("status")]
    public short? Status { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
