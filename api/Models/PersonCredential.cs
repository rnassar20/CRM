using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_credentials")]
public class PersonCredential
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Credential")]
    public Person Person { get; set; } = null!;

    /// <summary>was LogStr — login username</summary>
    [Column("username")]
    [MaxLength(30)]
    public string? Username { get; set; }

    /// <summary>BCrypt hash.</summary>
    [Column("password_hash")]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = null!;

    [Column("verification_code")]
    [MaxLength(30)]
    public string? VerificationCode { get; set; }

    [Column("def_lang")]
    public short? DefLang { get; set; }

    [Column("def_form")]
    public short? DefForm { get; set; }

    [Column("access_level")]
    public short AccessLevel { get; set; }

    [Column("must_reset")]
    public bool MustReset { get; set; } = false;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
