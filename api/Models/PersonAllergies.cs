using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_allergies")]
public class PersonAllergies
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Allergies")]
    public Person Person { get; set; } = null!;

    [Column("seq")]
    public short Seq { get; set; }

    [Column("created_date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Column("allergy_type")]
    public short? AllergyType { get; set; }

    [Column("item_id")]
    public int? ItemId { get; set; }

    [Column("description")]
    [MaxLength(255)]
    public string? Description { get; set; }

    [Column("ingredient_id")]
    public int? IngredientId { get; set; }

    [Column("ingredient_desc")]
    [MaxLength(255)]
    public string? IngredientDesc { get; set; }

    [Column("status")]
    public short? Status { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
