using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_communication")]
public class PersonCommunication
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Communications")]
    public Person Person { get; set; } = null!;

    [Column("first_visit")]
    public DateTime? FirstVisit { get; set; }

    [Column("com_phone")]
    public short ComPhone { get; set; } = 0;

    [Column("com_sms")]
    public short ComSms { get; set; } = 0;

    [Column("com_email")]
    public short ComEmail { get; set; } = 0;

    /// <summary>← drives CRM AllowWhatsApp</summary>
    [Column("com_whapp")]
    public short ComWhApp { get; set; } = 0;

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
