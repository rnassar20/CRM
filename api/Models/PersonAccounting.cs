using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_accounting")]
public class PersonAccounting
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Accountings")]
    public Person Person { get; set; } = null!;

    [Column("person_type")]
    public int PersonType { get; set; }

    [Column("seq")]
    public short Seq { get; set; }

    [Column("effective_date")]
    public DateTime EffectiveDate { get; set; }

    [Column("currency")]
    public short? Currency { get; set; }

    [Column("period")]
    public short? Period { get; set; }

    [Column("amount")]
    public decimal? Amount { get; set; }

    [Column("discount")]
    public decimal? Discount { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
