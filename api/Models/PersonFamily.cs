using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_family")]
public class PersonFamily
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("FamilyLinks")]
    public Person Person { get; set; } = null!;

    [Column("seq")]
    public short Seq { get; set; }

    /// <summary>FK ew_set page='FAMLNK'</summary>
    [Column("fam_lnk")]
    public short? FamLnk { get; set; }

    /// <summary>references another person.id</summary>
    [Column("fam_id")]
    public int? FamId { get; set; }

    [Column("fam_st")]
    public short? FamSt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
