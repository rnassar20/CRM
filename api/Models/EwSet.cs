using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("ew_set")]
public class EwSet
{
    [Key, Column("page", Order = 1)]
    [MaxLength(5)]
    public string Page { get; set; } = null!;

    [Key, Column("pscode", Order = 2)]
    [MaxLength(5)]
    public string Pscode { get; set; } = null!;

    [Column("uscode")]
    [MaxLength(5)]
    public string? Uscode { get; set; }

    [Column("description")]
    [MaxLength(255)]
    public string? Description { get; set; }

    [Column("status")]
    public short? Status { get; set; }

    [Column("usref")]
    public int? Usref { get; set; }

    [Column("descref")]
    public int? Descref { get; set; }
}
