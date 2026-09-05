using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("login_attempts")]
public class LoginAttempt
{
    [Key, Column("id")]
    public int Id { get; set; }

    /// <summary>person_id when the email/username matched a person; null for unknown.</summary>
    [Column("person_id")]
    public int? PersonId { get; set; }

    [Column("email")]
    [MaxLength(320)]
    public string? Email { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("success")]
    public bool Success { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
