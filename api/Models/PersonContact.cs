using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_contacts")]
public class PersonContact
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Contacts")]
    public Person Person { get; set; } = null!;

    [Column("seq")]
    public short Seq { get; set; }

    [Column("first_name")]
    [MaxLength(50)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [MaxLength(50)]
    public string? LastName { get; set; }

    [Column("job_pos")]
    [MaxLength(50)]
    public string? JobPos { get; set; }

    [Column("phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    [Column("phone_ext")]
    [MaxLength(10)]
    public string? PhoneExt { get; set; }

    [Column("mobile")]
    [MaxLength(50)]
    public string? Mobile { get; set; }

    [Column("email")]
    [MaxLength(50)]
    public string? Email { get; set; }

    /// <summary>contactable? maps to CRM AllowWhatsApp</summary>
    [Column("connect")]
    public short? Connect { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
