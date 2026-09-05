using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("ew_profile")]
public class Profile
{
    [Key, Column("phcyid")]
    public int Id { get; set; }

    [Column("phtype")]
    public short? PhType { get; set; }

    [Column("description")]
    [MaxLength(255)]
    public string? Description { get; set; }

    [Column("ctfstname")]
    [MaxLength(50)]
    public string? ContactFirstName { get; set; }

    [Column("ctmidname")]
    [MaxLength(50)]
    public string? ContactMiddleName { get; set; }

    [Column("ctlstname")]
    [MaxLength(50)]
    public string? ContactLastName { get; set; }

    [Column("title")]
    public short? Title { get; set; }

    [Column("country")]
    public short? Country { get; set; }

    [Column("territory")]
    public short? Territory { get; set; }

    [Column("city")]
    public short? City { get; set; }

    [Column("street")]
    [MaxLength(255)]
    public string? Street { get; set; }

    [Column("building")]
    [MaxLength(50)]
    public string? Building { get; set; }

    [Column("floor")]
    [MaxLength(50)]
    public string? Floor { get; set; }

    [Column("pobox")]
    [MaxLength(50)]
    public string? POBOX { get; set; }

    [Column("phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    [Column("mobile")]
    [MaxLength(50)]
    public string? Mobile { get; set; }

    [Column("fax")]
    [MaxLength(50)]
    public string? Fax { get; set; }

    [Column("email")]
    [MaxLength(50)]
    public string? Email { get; set; }

    [Column("classtyp")]
    public short? ClassTyp { get; set; }

    [Column("app_type")]
    public short AppType { get; set; } = (short)4;

    [Column("phsid")]
    public int? PHSID { get; set; }

    [Column("remarks")]
    [MaxLength(255)]
    public string? Remarks { get; set; }

    [Column("moh_num")]
    [MaxLength(20)]
    public string? MOHNum { get; set; }

    [Column("mof_num")]
    [MaxLength(20)]
    public string? MOFNum { get; set; }

    [Column("vat_num")]
    [MaxLength(20)]
    public string? VATNum { get; set; }

    [Column("ssn_num")]
    [MaxLength(20)]
    public string? SSNNum { get; set; }

    [Column("ph_lic_num")]
    [MaxLength(20)]
    public string? PhLicNum { get; set; }

    [Column("ph_lic_date")]
    public DateTime? PhLicDate { get; set; }

    [Column("oper_id")]
    public int? OperId { get; set; }

    [Column("form_id")]
    public int? FormId { get; set; }

    [Column("timest")]
    public DateTime? TimeSt { get; set; }

    [InverseProperty("Profile")]
    public ICollection<ProfileSetting> Settings { get; set; } = [];

    [InverseProperty("Profile")]
    public ICollection<Person> Persons { get; set; } = [];
}
