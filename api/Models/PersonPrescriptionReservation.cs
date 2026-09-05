using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_prescription_reservation")]
public class PersonPrescriptionReservation
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Prescriptions")]
    public Person Person { get; set; } = null!;

    [Column("effective_date")]
    public DateTime EffectiveDate { get; set; }

    [Column("expiry_date")]
    public DateTime? ExpiryDate { get; set; }

    [Column("ben_id")]
    public int? BenId { get; set; }

    [Column("item_id")]
    public int? ItemId { get; set; }

    [Column("box_qty")]
    public short? BoxQty { get; set; }

    [Column("reservation_period")]
    public short? ReservationPeriod { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
