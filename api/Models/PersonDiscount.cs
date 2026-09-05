using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_discounts")]
public class PersonDiscount
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Discounts")]
    public Person Person { get; set; } = null!;

    [Column("disc_bas_adv")]
    public short? DiscBasAdv { get; set; }

    [Column("disc_cost")]
    public short? DiscCost { get; set; }

    [Column("disc_val")]
    public decimal? DiscVal { get; set; }

    [Column("disc_type")]
    public short? DiscType { get; set; }

    [Column("disc_exc")]
    public short? DiscExc { get; set; }

    [Column("cost_inv")]
    public short? CostInv { get; set; }

    [Column("disc_str")]
    public short? DiscStr { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
