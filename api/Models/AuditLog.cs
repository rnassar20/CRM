using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("audit_log")]
public class AuditLog
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int? PersonId { get; set; }

    /// <summary>login_success | login_failed | logout | forgot_password_requested |
    /// password_reset_initiated | password_reset_completed | password_changed |
    /// user_created | user_deactivated | user_activated | credential_locked</summary>
    [Column("event_type")]
    [MaxLength(60)]
    public string EventType { get; set; } = null!;

    [Column("detail")]
    public string? Detail { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
