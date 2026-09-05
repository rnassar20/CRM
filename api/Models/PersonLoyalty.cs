using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_loyalty")]
public class PersonLoyalty
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Loyalty")]
    public Person Person { get; set; } = null!;

    [Column("loy_id")]
    [MaxLength(50)]
    public string? LoyId { get; set; }

    [Column("loy_card")]
    [MaxLength(50)]
    public string? LoyCard { get; set; }

    [Column("loy_points")]
    public decimal? LoyPoints { get; set; }

    [Column("loy_disc")]
    public decimal? LoyDisc { get; set; }

    [Column("loy_eff_date")]
    public DateTime? LoyEffDate { get; set; }

    [Column("loy_mod_date")]
    public DateTime? LoyModDate { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
