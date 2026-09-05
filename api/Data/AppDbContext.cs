using Crm.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // === existing CRM module tables (keep) ===
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<SubscriptionPlan> Plans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<FollowUp> FollowUps => Set<FollowUp>();
    public DbSet<ClientContact> ClientContacts => Set<ClientContact>();
    public DbSet<WhatsAppMessage> WhatsAppMessages => Set<WhatsAppMessage>();

    // === unified person core ===
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ProfileSetting> ProfileSettings => Set<ProfileSetting>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<PersonCredential> PersonCredentials => Set<PersonCredential>();
    public DbSet<PersonContact> PersonContacts => Set<PersonContact>();
    public DbSet<PersonCommunication> PersonCommunications => Set<PersonCommunication>();
    public DbSet<PersonDiscount> PersonDiscounts => Set<PersonDiscount>();
    public DbSet<PersonLoyalty> PersonLoyalties => Set<PersonLoyalty>();
    public DbSet<PersonFamily> PersonFamilies => Set<PersonFamily>();
    public DbSet<PersonImage> PersonImages => Set<PersonImage>();

    // === clinical / accounting module tables ===
    public DbSet<PersonClinical> PersonClinicalRecords => Set<PersonClinical>();
    public DbSet<PersonClinicalDetail> PersonClinicalDetails => Set<PersonClinicalDetail>();
    public DbSet<PersonAllergies> PersonAllergies => Set<PersonAllergies>();
    public DbSet<PersonSymptoms> PersonSymptoms => Set<PersonSymptoms>();
    public DbSet<PersonInsurance> PersonInsurances => Set<PersonInsurance>();
    public DbSet<PersonPrescriptionReservation> PersonPrescriptionReservations => Set<PersonPrescriptionReservation>();
    public DbSet<PersonAccounting> PersonAccountings => Set<PersonAccounting>();

    // === CRM extension + auth / audit ===
    public DbSet<CrmClientExtension> CrmClientExtensions => Set<CrmClientExtension>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // === lookup ===
    public DbSet<EwSet> EwSets => Set<EwSet>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // --- ew_set (composite PK) ---
        mb.Entity<EwSet>(e =>
        {
            e.HasKey(x => new { x.Page, x.Pscode });
            e.Property(x => x.Page).HasMaxLength(5).HasColumnName("page");
            e.Property(x => x.Pscode).HasMaxLength(5).HasColumnName("pscode");
            e.Property(x => x.Uscode).HasMaxLength(5).HasColumnName("uscode");
            e.Property(x => x.Description).HasMaxLength(255).HasColumnName("description");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Usref).HasColumnName("usref");
            e.Property(x => x.Descref).HasColumnName("descref");
        });

        // --- profile (existing table, extend with app_type) ---
        mb.Entity<Profile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("phcyid");
            e.Property(x => x.PhType).HasColumnName("phtype");
            e.Property(x => x.Description).HasMaxLength(255).HasColumnName("description");
            e.Property(x => x.ContactFirstName).HasMaxLength(50).HasColumnName("ctfstname");
            e.Property(x => x.ContactMiddleName).HasMaxLength(50).HasColumnName("ctmidname");
            e.Property(x => x.ContactLastName).HasMaxLength(50).HasColumnName("ctlstname");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.Country).HasColumnName("country");
            e.Property(x => x.Territory).HasColumnName("territory");
            e.Property(x => x.City).HasColumnName("city");
            e.Property(x => x.Street).HasMaxLength(255).HasColumnName("street");
            e.Property(x => x.Building).HasMaxLength(50).HasColumnName("building");
            e.Property(x => x.Floor).HasMaxLength(50).HasColumnName("floor");
            e.Property(x => x.POBOX).HasMaxLength(50).HasColumnName("pobox");
            e.Property(x => x.Phone).HasMaxLength(50).HasColumnName("phone");
            e.Property(x => x.Mobile).HasMaxLength(50).HasColumnName("mobile");
            e.Property(x => x.Fax).HasMaxLength(50).HasColumnName("fax");
            e.Property(x => x.Email).HasMaxLength(50).HasColumnName("email");
            e.Property(x => x.ClassTyp).HasColumnName("classtyp");
            e.Property(x => x.AppType).HasColumnName("app_type");
            e.Property(x => x.PHSID).HasColumnName("phsid");
            e.Property(x => x.Remarks).HasMaxLength(255).HasColumnName("remarks");
            e.Property(x => x.MOHNum).HasMaxLength(20).HasColumnName("moh_num");
            e.Property(x => x.MOFNum).HasMaxLength(20).HasColumnName("mof_num");
            e.Property(x => x.VATNum).HasMaxLength(20).HasColumnName("vat_num");
            e.Property(x => x.SSNNum).HasMaxLength(20).HasColumnName("ssn_num");
            e.Property(x => x.PhLicNum).HasMaxLength(20).HasColumnName("ph_lic_num");
            e.Property(x => x.PhLicDate).HasColumnName("ph_lic_date");
            e.Property(x => x.OperId).HasColumnName("oper_id");
            e.Property(x => x.FormId).HasColumnName("form_id");
            e.Property(x => x.TimeSt).HasColumnName("timest");

            e.HasMany(x => x.Settings).WithOne(s => s.Profile).HasForeignKey(s => s.ProfileId);
            e.HasMany(x => x.Persons).WithOne(p => p.Profile).HasForeignKey(p => p.ProfileId);

            // Legacy billing tables use "SubscriptionPlans" / "Subscriptions" names
            mb.Entity<SubscriptionPlan>().ToTable("SubscriptionPlans");
            mb.Entity<SubscriptionPlan>().Property(p => p.Cycle)
                .HasConversion<string>();
            mb.Entity<Subscription>().ToTable("Subscriptions");

            e.HasIndex(x => x.AppType);
            e.HasIndex(x => x.Email).HasFilter("email IS NOT NULL");
        });

        // --- profile settings (composite PK) ---
        mb.Entity<ProfileSetting>(e =>
        {
            e.HasKey(x => new { x.ProfileId, x.TermId });
            e.Property(x => x.ProfileId).HasColumnName("phcyid");
            e.Property(x => x.TermId).HasColumnName("termid");
            e.Property(x => x.RcpTemp).HasMaxLength(100).HasColumnName("rcp_temp");
            e.Property(x => x.RcpPrint).HasMaxLength(50).HasColumnName("rcp_print");
            e.Property(x => x.RcpPrnSal).HasColumnName("rcp_prn_sal");
            e.Property(x => x.SsnTemp).HasMaxLength(100).HasColumnName("ssn_temp");
            e.Property(x => x.SsnPrint).HasMaxLength(50).HasColumnName("ssn_print");
            e.Property(x => x.LblTemp).HasMaxLength(100).HasColumnName("lbl_temp");
            e.Property(x => x.LblPrint).HasMaxLength(50).HasColumnName("lbl_print");
            e.Property(x => x.BcdTemp).HasMaxLength(100).HasColumnName("bcd_temp");
            e.Property(x => x.BcdPrint).HasMaxLength(50).HasColumnName("bcd_print");
            e.Property(x => x.Curr1).HasColumnName("curr1");
            e.Property(x => x.Curr2).HasColumnName("curr2");
            e.Property(x => x.FxRate).HasColumnName("fx_rate");
            e.Property(x => x.VatRate).HasColumnName("vat_rate");
            e.Property(x => x.OpnCash).HasColumnName("opn_cash");
            e.Property(x => x.SsnRsv).HasColumnName("ssn_rsv");
            e.Property(x => x.DrwCom).HasMaxLength(50).HasColumnName("drw_com");
            e.Property(x => x.BckTime).HasMaxLength(20).HasColumnName("bck_time");
            e.Property(x => x.BckCnt).HasColumnName("bck_cnt");
            e.Property(x => x.BckPath).HasMaxLength(255).HasColumnName("bck_path");
            e.Property(x => x.OperId).HasColumnName("oper_id");
            e.Property(x => x.FormId).HasColumnName("form_id");
            e.Property(x => x.TimeSt).HasColumnName("timest");
        });

        // --- unified person ---
        mb.Entity<Person>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProfileId).HasColumnName("profile_id");
            e.Property(x => x.PersonType).HasColumnName("person_type");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.FirstName).HasMaxLength(50).HasColumnName("first_name");
            e.Property(x => x.MiddleName).HasMaxLength(50).HasColumnName("middle_name");
            e.Property(x => x.LastName).HasMaxLength(50).HasColumnName("last_name");
            e.Property(x => x.CountryId).HasColumnName("country_id");
            e.Property(x => x.TerritoryId).HasColumnName("territory_id");
            e.Property(x => x.CityId).HasColumnName("city_id");
            e.Property(x => x.Street).HasMaxLength(255).HasColumnName("street");
            e.Property(x => x.Building).HasMaxLength(50).HasColumnName("building");
            e.Property(x => x.Floor).HasMaxLength(50).HasColumnName("floor");
            e.Property(x => x.Pobox).HasMaxLength(50).HasColumnName("pobox");
            e.Property(x => x.Phone).HasMaxLength(50).HasColumnName("phone");
            e.Property(x => x.Mobile).HasMaxLength(50).HasColumnName("mobile");
            e.Property(x => x.Fax).HasMaxLength(50).HasColumnName("fax");
            e.Property(x => x.Email).HasMaxLength(50).HasColumnName("email");
            e.Property(x => x.ClassTyp).HasColumnName("class_typ");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(10);
            e.Property(x => x.MaritalStatus).HasColumnName("marital_status");
            e.Property(x => x.RegistryNo).HasMaxLength(20).HasColumnName("registry_no");
            e.Property(x => x.Dob).HasColumnName("dob");
            e.Property(x => x.Gender).HasColumnName("gender");
            e.Property(x => x.Remarks).HasMaxLength(255).HasColumnName("remarks");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Credential).WithOne(c => c.Person).HasForeignKey<PersonCredential>(c => c.PersonId);
            e.HasMany(x => x.Contacts).WithOne(c => c.Person).HasForeignKey(c => c.PersonId);
            e.HasOne(x => x.Communications).WithOne(c => c.Person).HasForeignKey<PersonCommunication>(c => c.PersonId);
            e.HasMany(x => x.Discounts).WithOne(d => d.Person).HasForeignKey(d => d.PersonId);
            e.HasMany(x => x.Loyalty).WithOne(l => l.Person).HasForeignKey(l => l.PersonId);
            e.HasMany(x => x.FamilyLinks).WithOne(f => f.Person).HasForeignKey(f => f.PersonId);
            e.HasMany(x => x.Images).WithOne(i => i.Person).HasForeignKey(i => i.PersonId);

            e.HasIndex(x => x.ProfileId);
            e.HasIndex(x => x.PersonType);
            e.HasIndex(x => x.Email).HasFilter("email IS NOT NULL");
            e.HasIndex(x => x.Status);
        });

        // --- person credential (BCrypt) ---
        mb.Entity<PersonCredential>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Username).HasMaxLength(30).HasColumnName("username");
            e.Property(x => x.PasswordHash).HasMaxLength(255).HasColumnName("password_hash");
            e.Property(x => x.VerificationCode).HasMaxLength(30).HasColumnName("verification_code");
            e.Property(x => x.DefLang).HasColumnName("def_lang");
            e.Property(x => x.DefForm).HasColumnName("def_form");
            e.Property(x => x.AccessLevel).HasColumnName("access_level");
            e.Property(x => x.MustReset).HasColumnName("must_reset").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasIndex(x => x.Username).HasFilter("username IS NOT NULL");
        });

        // --- person contact (composite unique) ---
        mb.Entity<PersonContact>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.FirstName).HasMaxLength(50).HasColumnName("first_name");
            e.Property(x => x.LastName).HasMaxLength(50).HasColumnName("last_name");
            e.Property(x => x.JobPos).HasMaxLength(50).HasColumnName("job_pos");
            e.Property(x => x.Phone).HasMaxLength(50).HasColumnName("phone");
            e.Property(x => x.PhoneExt).HasMaxLength(10).HasColumnName("phone_ext");
            e.Property(x => x.Mobile).HasMaxLength(50).HasColumnName("mobile");
            e.Property(x => x.Email).HasMaxLength(50).HasColumnName("email");
            e.Property(x => x.Connect).HasColumnName("connect");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasIndex(x => new { x.PersonId, x.Seq }).IsUnique().HasDatabaseName("idx_person_contacts_person_seq");
        });

        // --- person communication (one-to-one) ---
        mb.Entity<PersonCommunication>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.FirstVisit).HasColumnName("first_visit");
            e.Property(x => x.ComPhone).HasColumnName("com_phone");
            e.Property(x => x.ComSms).HasColumnName("com_sms");
            e.Property(x => x.ComEmail).HasColumnName("com_email");
            e.Property(x => x.ComWhApp).HasColumnName("com_whapp");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithOne(p => p.Communications).HasForeignKey<PersonCommunication>(x => x.PersonId);
        });

        // --- person discount ---
        mb.Entity<PersonDiscount>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.DiscBasAdv).HasColumnName("disc_bas_adv");
            e.Property(x => x.DiscCost).HasColumnName("disc_cost");
            e.Property(x => x.DiscVal).HasColumnName("disc_val");
            e.Property(x => x.DiscType).HasColumnName("disc_type");
            e.Property(x => x.DiscExc).HasColumnName("disc_exc");
            e.Property(x => x.CostInv).HasColumnName("cost_inv");
            e.Property(x => x.DiscStr).HasColumnName("disc_str");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.Discounts).HasForeignKey(x => x.PersonId);
        });

        // --- person loyalty ---
        mb.Entity<PersonLoyalty>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.LoyId).HasMaxLength(50).HasColumnName("loy_id");
            e.Property(x => x.LoyCard).HasMaxLength(50).HasColumnName("loy_card");
            e.Property(x => x.LoyPoints).HasColumnName("loy_points");
            e.Property(x => x.LoyDisc).HasColumnName("loy_disc");
            e.Property(x => x.LoyEffDate).HasColumnName("loy_eff_date");
            e.Property(x => x.LoyModDate).HasColumnName("loy_mod_date");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.Loyalty).HasForeignKey(x => x.PersonId);
        });

        // --- person family (composite unique) ---
        mb.Entity<PersonFamily>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.FamLnk).HasColumnName("fam_lnk");
            e.Property(x => x.FamId).HasColumnName("fam_id");
            e.Property(x => x.FamSt).HasColumnName("fam_st");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.FamilyLinks).HasForeignKey(x => x.PersonId);
            e.HasIndex(x => new { x.PersonId, x.Seq }).IsUnique().HasDatabaseName("idx_person_family_person_seq");
        });

        // --- person image (BYTEA) ---
        mb.Entity<PersonImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.FsData).HasColumnName("fs_data");
            e.Property(x => x.FsDataGuid).HasColumnName("fs_data_guid");
            e.Property(x => x.FsDateTime).HasColumnName("fs_date_time");

            e.HasOne(x => x.Person).WithMany(p => p.Images).HasForeignKey(x => x.PersonId);
            e.HasIndex(x => x.FsDataGuid).IsUnique().HasDatabaseName("idx_person_images_guid");
        });

        // --- clinical tables ---
        mb.Entity<PersonClinical>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.VisitDate).HasColumnName("visit_date");
            e.Property(x => x.PersonsWeight).HasColumnName("persons_weight").HasColumnType("numeric(5,2)");
            e.Property(x => x.PersonsHeight).HasColumnName("persons_height").HasColumnType("numeric(5,2)");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.ClinicalRecords).HasForeignKey(x => x.PersonId);
            e.HasIndex(x => new { x.PersonId, x.Seq }).IsUnique().HasDatabaseName("idx_person_clinical_person_seq");
        });

        mb.Entity<PersonClinicalDetail>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.ClinicalId).HasColumnName("clinical_id");
            e.Property(x => x.ClinicalType).HasColumnName("clinical_type");
            e.Property(x => x.Value).HasMaxLength(20).HasColumnName("value");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.ClinicalDetails).HasForeignKey(x => x.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.PersonId, x.Seq, x.ClinicalId })
                .IsUnique().HasDatabaseName("idx_person_clinical_detail_person_seq_id");
        });

        mb.Entity<PersonAllergies>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.CreatedDate).HasColumnName("created_date");
            e.Property(x => x.AllergyType).HasColumnName("allergy_type");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.Description).HasMaxLength(255).HasColumnName("description");
            e.Property(x => x.IngredientId).HasColumnName("ingredient_id");
            e.Property(x => x.IngredientDesc).HasMaxLength(255).HasColumnName("ingredient_desc");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.Allergies).HasForeignKey(x => x.PersonId);
            e.HasIndex(x => new { x.PersonId, x.Seq }).IsUnique().HasDatabaseName("idx_person_allergies_person_seq");
        });

        mb.Entity<PersonSymptoms>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.SymptomSeq).HasColumnName("symptom_seq");
            e.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            e.Property(x => x.SymptomId).HasColumnName("symptom_id");
            e.Property(x => x.Remarks).HasMaxLength(255).HasColumnName("remarks");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.Symptoms).HasForeignKey(x => x.PersonId);
            e.HasIndex(x => new { x.PersonId, x.SymptomSeq }).IsUnique().HasDatabaseName("idx_person_symptoms_person_seq");
        });

        mb.Entity<PersonInsurance>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.InsuranceType).HasColumnName("insurance_type");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            e.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
            e.Property(x => x.OrgId).HasColumnName("org_id");
            e.Property(x => x.BenOrgId).HasMaxLength(50).HasColumnName("ben_org_id");
            e.Property(x => x.Covered).HasColumnName("covered");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.Insurances).HasForeignKey(x => x.PersonId);
            e.HasIndex(x => new { x.PersonId, x.InsuranceType, x.Seq })
                .IsUnique().HasDatabaseName("idx_person_insurance_person_type_seq");
        });

        mb.Entity<PersonPrescriptionReservation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            e.Property(x => x.ExpiryDate).HasColumnName("expiry_date");
            e.Property(x => x.BenId).HasColumnName("ben_id");
            e.Property(x => x.ItemId).HasColumnName("item_id");
            e.Property(x => x.BoxQty).HasColumnName("box_qty");
            e.Property(x => x.ReservationPeriod).HasColumnName("reservation_period");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.Prescriptions).HasForeignKey(x => x.PersonId);
            e.HasIndex(x => new { x.PersonId, x.EffectiveDate })
                .IsUnique().HasDatabaseName("idx_person_prescription_reservation_person_effdate");
        });

        mb.Entity<PersonAccounting>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.PersonType).HasColumnName("person_type");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.EffectiveDate).HasColumnName("effective_date");
            e.Property(x => x.Currency).HasColumnName("currency");
            e.Property(x => x.Period).HasColumnName("period");
            e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(15,2)");
            e.Property(x => x.Discount).HasColumnName("discount").HasColumnType("numeric(5,2)");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithMany(p => p.Accountings).HasForeignKey(x => x.PersonId);
            e.HasIndex(x => new { x.PersonId, x.PersonType, x.Seq })
                .IsUnique().HasDatabaseName("idx_person_accounting_person_type_seq");
        });

        // --- CRM client extension ---
        mb.Entity<CrmClientExtension>(e =>
        {
            e.HasKey(x => x.PersonId);
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Status).HasMaxLength(30).HasColumnName("status");
            e.Property(x => x.ClientType).HasMaxLength(30).HasColumnName("client_type");
            e.Property(x => x.Notes).HasColumnName("notes");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

            e.HasOne(x => x.Person).WithOne(p => p.CrmExtension).HasForeignKey<CrmClientExtension>(x => x.PersonId);
        });

        // --- login attempts (lockout) ---
        mb.Entity<LoginAttempt>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.Email).HasMaxLength(320).HasColumnName("email");
            e.Property(x => x.IpAddress).HasMaxLength(45).HasColumnName("ip_address");
            e.Property(x => x.Success).HasColumnName("success");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasIndex(x => new { x.Email, x.CreatedAt }).HasDatabaseName("idx_login_attempts_email_created");
            e.HasIndex(x => new { x.IpAddress, x.CreatedAt }).HasDatabaseName("idx_login_attempts_ip_created");
        });

        // --- audit log ---
        mb.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.PersonId).HasColumnName("person_id");
            e.Property(x => x.EventType).HasMaxLength(60).HasColumnName("event_type");
            e.Property(x => x.Detail).HasColumnName("detail");
            e.Property(x => x.IpAddress).HasMaxLength(45).HasColumnName("ip_address");
            e.Property(x => x.UserAgent).HasMaxLength(500).HasColumnName("user_agent");
            e.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasIndex(x => new { x.PersonId, x.EventType }).HasDatabaseName("idx_audit_log_person_event");
            e.HasIndex(x => new { x.EventType, x.CreatedAt }).HasDatabaseName("idx_audit_log_event_created");
        });
    }
}
