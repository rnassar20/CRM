using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("person_images")]
public class PersonImage
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("person_id")]
    public int PersonId { get; set; }
    [ForeignKey("PersonId")]
    [InverseProperty("Images")]
    public Person Person { get; set; } = null!;

    [Column("fs_data")]
    public byte[]? FsData { get; set; }

    [Column("fs_data_guid")]
    public Guid FsDataGuid { get; set; } = Guid.NewGuid();

    [Column("fs_date_time")]
    public DateTime FsDateTime { get; set; } = DateTime.UtcNow;
}
