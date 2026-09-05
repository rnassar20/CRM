using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_clinical")]
public class PersonClinical
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("ClinicalRecords")]
    public Person Person { get; set; } = null!;

    [Column("seq")]
    public short Seq { get; set; }

    [Column("visit_date")]
    public DateTime VisitDate { get; set; }

    [Column("persons_weight")]
    public decimal? PersonsWeight { get; set; }

    [Column("persons_height")]
    public decimal? PersonsHeight { get; set; }

    [Column("status")]
    public short? Status { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
