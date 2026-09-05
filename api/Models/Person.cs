using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Crm.Api.Models;

[Table("persons")]
public class Person
{
    [Key, Column("id")]
    public int Id { get; set; }

    [Column("profile_id")]
    public int ProfileId { get; set; }
    [ForeignKey("ProfileId")]
    public Profile Profile { get; set; } = null!;

    [Column("person_type")]
    public short PersonType { get; set; }

    [Column("title")]
    public short? Title { get; set; }

    [Column("first_name")]
    [MaxLength(50)]
    public string? FirstName { get; set; }

    [Column("middle_name")]
    [MaxLength(50)]
    public string? MiddleName { get; set; }

    [Column("last_name")]
    [MaxLength(50)]
    public string? LastName { get; set; }

    [Column("country_id")]
    public int? CountryId { get; set; }

    [Column("territory_id")]
    public int? TerritoryId { get; set; }

    [Column("city_id")]
    public int? CityId { get; set; }

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
    public string? Pobox { get; set; }

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

    [Column("class_typ")]
    public short? ClassTyp { get; set; }

    [Column("status")]
    public string Status { get; set; } = "1";

    [Column("marital_status")]
    public short? MaritalStatus { get; set; }

    [Column("registry_no")]
    [MaxLength(20)]
    public string? RegistryNo { get; set; }

    [Column("dob")]
    public DateTime? Dob { get; set; }

    [Column("gender")]
    public short? Gender { get; set; }

    [Column("remarks")]
    [MaxLength(255)]
    public string? Remarks { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [InverseProperty("Person")]
    public PersonCredential? Credential { get; set; }

    [InverseProperty("Person")]
    public ICollection<PersonContact> Contacts { get; set; } = [];

    [InverseProperty("Person")]
    public PersonCommunication? Communications { get; set; }

    [InverseProperty("Person")]
    public ICollection<PersonDiscount> Discounts { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonLoyalty> Loyalty { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonFamily> FamilyLinks { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonImage> Images { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonClinical> ClinicalRecords { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonClinicalDetail> ClinicalDetails { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonAllergies> Allergies { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonSymptoms> Symptoms { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonInsurance> Insurances { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonPrescriptionReservation> Prescriptions { get; set; } = [];

    [InverseProperty("Person")]
    public ICollection<PersonAccounting> Accountings { get; set; } = [];

    [InverseProperty("Person")]
    public CrmClientExtension? CrmExtension { get; set; }
}
