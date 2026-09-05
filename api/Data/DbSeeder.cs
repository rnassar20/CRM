using System.Security.Cryptography;
using System.Text;
using Crm.Api.Models;

namespace Crm.Api.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Persons.Any()) return; // already seeded

        // ---------- lookups: ensure core ew_set codes exist ----------
        EnsureEwSet(db, "APPTYP", "1", "ERP — Pharmacy");
        EnsureEwSet(db, "APPTYP", "2", "ERP — Shop / Market");
        EnsureEwSet(db, "APPTYP", "3", "ERP — Restaurant");
        EnsureEwSet(db, "APPTYP", "4", "ERP — Company");
        EnsureEwSet(db, "APPTYP", "5", "CRM");
        EnsureEwSet(db, "APPTYP", "6", "Clinical Management");
        EnsureEwSet(db, "APPTYP", "7", "SPA / Hair Salon / Garage");

        EnsureEwSet(db, "TITLE", "1", "Mr");
        EnsureEwSet(db, "TITLE", "2", "Mme");
        EnsureEwSet(db, "TITLE", "3", "Ms");
        EnsureEwSet(db, "TITLE", "4", "Dr");

        EnsureEwSet(db, "GENDER", "1", "Male");
        EnsureEwSet(db, "GENDER", "2", "Female");

        EnsureEwSet(db, "MRTST", "1", "Single");
        EnsureEwSet(db, "MRTST", "2", "Married");

        EnsureEwSet(db, "DISCTYP", "1", "Basic Discount");
        EnsureEwSet(db, "DISCTYP", "2", "Advanced Discount");

        EnsureEwSet(db, "CLNTYP", "1", "Blood Pressure");
        EnsureEwSet(db, "CLNTYP", "2", "Total Cholesterol");
        EnsureEwSet(db, "ALGTYP", "1", "Medicine");
        EnsureEwSet(db, "ALGTYP", "2", "Ingredient");
        EnsureEwSet(db, "FAMLNK", "1", "Father");
        EnsureEwSet(db, "FAMLNK", "2", "Mother");
        EnsureEwSet(db, "FAMLNK", "3", "Spouse");
        EnsureEwSet(db, "FAMLNK", "4", "Child");

        // ---------- profile: CRM (app_type = 5) ----------
        var crmProfile = new Profile
        {
            Description = "ERP CRM — web app",
            AppType = 5,
            ContactFirstName = "CRM Admin",
            ContactLastName = "Office",
            Email = "admin@crm.local",
            PhLicNum = "CRM-001"
        };
        db.Profiles.Add(crmProfile);
        db.SaveChanges();

        var adminPersonId = 0;

        // ---------- admin person + credential (employee, person_type=1) ----------
        var adminPerson = new Person
        {
            ProfileId = crmProfile.Id,
            PersonType = 1, // Employee
            Title = 4,     // Dr
            FirstName = "System",
            LastName = "Administrator",
            Email = "admin@crm.local",
            Status = "1",
            ClassTyp = 1,  // Admin
            CreatedBy = 0
        };
        db.Persons.Add(adminPerson);
        db.SaveChanges();
        adminPersonId = adminPerson.Id;

        var adminCred = new PersonCredential
        {
            PersonId = adminPersonId,
            Username = "admin",
            PasswordHash = BCryptHasher.Hash("Admin@123"),
            AccessLevel = 1, // Admin
            MustReset = false
        };
        db.PersonCredentials.Add(adminCred);

        // ---------- demo clients as persons (person_type=2, CRM profile) ----------
        var clients = new[]
        {
            new { name = "Al-Shifa Pharmacy", contact = "Ahmed Hassan", phone = "+201****4567", email = "info@alshifa.example", city = "Cairo", type = "Pharmacy", status = "Subscribed", mobile = "+201****4567" },
            new { name = "Sunshine Gifts", contact = "Mona Adel", phone = "+201****4321", email = (string?)null, city = "Giza", type = "GiftShop", status = "Interested", mobile = "+201****4321" },
            new { name = "Dr. Sara Clinic", contact = "Dr. Sara Ibrahim", phone = "+201****3333", email = (string?)null, city = "Cairo", type = "DoctorClinic", status = "Potential", mobile = "+201****3333" }
        };

        var clientIds = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var c in clients)
        {
            var person = new Person
            {
                ProfileId = crmProfile.Id,
                PersonType = 2, // Client
                Title = c.name.StartsWith("Dr.") ? (short)4 : (short)1,
                FirstName = c.contact.Split(' ')[0],
                LastName = string.Join(" ", c.contact.Split(' ').Skip(1)),
                Phone = c.phone,
                Mobile = c.mobile,
                Email = c.email,
                CityId = 1, // placeholder
                Status = "1",
                ClassTyp = c.type switch { "Pharmacy" => 1, "GiftShop" => 2, "DoctorClinic" => 3, _ => 4 },
                CreatedBy = adminPersonId
            };
            db.Persons.Add(person);
            db.SaveChanges();
            clientIds[c.name] = person.Id;

            db.CrmClientExtensions.Add(new CrmClientExtension
            {
                PersonId = person.Id,
                Status = c.status,
                ClientType = c.type,
                CreatedBy = adminPersonId
            });

            db.PersonCommunications.Add(new PersonCommunication
            {
                PersonId = person.Id,
                ComPhone = 1,
                ComSms = 1,
                ComEmail = 1,
                ComWhApp = c.type == "Pharmacy" ? (short)1 : (short)0,
                CreatedBy = adminPersonId
            });
        }

        // ---------- secondary contacts for Al-Shifa Pharmacy ----------
        var pharmacyId = clientIds["Al-Shifa Pharmacy"];
        db.PersonContacts.AddRange(
            new PersonContact { PersonId = pharmacyId, Seq = 1, FirstName = "Khaled", LastName = "Mansour", JobPos = "Pharmacist on duty", Phone = "+201****4433", Mobile = "+201****4433", Email = "khaled@alshifa.example", Connect = 1, CreatedBy = adminPersonId },
            new PersonContact { PersonId = pharmacyId, Seq = 2, FirstName = "Nour", LastName = "El-Sayed", JobPos = "Accountant", Phone = "+201****8877", Mobile = "+201****8877", Connect = 1, CreatedBy = adminPersonId }
        );

        db.SaveChanges();

        // ---------- subscriptions (point at persons) ----------
        var today = DateTime.Today;
        var basicPlan = new SubscriptionPlan { Name = "Basic - Monthly", Cycle = BillingCycle.Monthly, Price = 120m, IsActive = true };
        var proPlan = new SubscriptionPlan { Name = "Pro - 1 Year", Cycle = BillingCycle.Yearly, Price = 2000m, IsActive = true };
        db.Plans.AddRange(basicPlan, proPlan);
        db.SaveChanges();

        db.Subscriptions.Add(new Subscription
        {
            ClientId = pharmacyId,
            PlanId = basicPlan.Id,
            Cycle = BillingCycle.Monthly,
            StartDate = today.AddDays(-30),
            ExpiryDate = today.AddDays(1),
            Price = basicPlan.Price,
            PaymentStatus = PaymentStatus.Unpaid
        });
        db.SaveChanges();

        // ---------- interactions + follow-ups + ticket ----------
        db.Interactions.AddRange(
            new Interaction { ClientId = clientIds["Sunshine Gifts"], Type = InteractionType.Call, Outcome = InteractionOutcome.Interested, Notes = "Asked for pricing sheet.", NextFollowUpAt = DateTime.Today.AddDays(3), UserId = adminPersonId },
            new Interaction { ClientId = clientIds["Dr. Sara Clinic"], Type = InteractionType.WhatsApp, Outcome = InteractionOutcome.CallbackRequested, Notes = "Receptionist reviews after 6 PM.", UserId = adminPersonId }
        );

        var ticket = new Ticket { ClientId = pharmacyId, Title = "Barcode printer not printing invoices", Description = "Printer works in test page.", Priority = TicketPriority.High, Status = TicketStatus.Open, CreatedById = adminPersonId };
        db.Tickets.Add(ticket);
        db.SaveChanges();

        db.FollowUps.AddRange(
            new FollowUp { ClientId = clientIds["Sunshine Gifts"], Title = "Send pricing sheet", Type = FollowUpType.Marketing, ScheduledAt = DateTime.Today.AddDays(3).AddHours(11), AssignedToId = adminPersonId, CreatedById = adminPersonId },
            new FollowUp { ClientId = clientIds["Dr. Sara Clinic"], Title = "First contact call", Type = FollowUpType.Marketing, ScheduledAt = DateTime.Today.AddHours(2), AssignedToId = adminPersonId, CreatedById = adminPersonId },
            new FollowUp { ClientId = pharmacyId, Title = $"Check ticket #{ticket.Id}", Type = FollowUpType.Support, TicketId = ticket.Id, ScheduledAt = DateTime.Today.AddDays(1).AddHours(14), AssignedToId = adminPersonId, CreatedById = adminPersonId }
        );

        db.SaveChanges();

        // ---------- clinical sample ----------
        db.PersonClinicalRecords.Add(new PersonClinical { PersonId = clientIds["Dr. Sara Clinic"], Seq = 1, VisitDate = DateTime.Today.AddDays(-30), PersonsWeight = 72.5m, PersonsHeight = 170.0m, Status = 1, CreatedBy = adminPersonId });
        db.PersonAllergies.Add(new PersonAllergies { PersonId = clientIds["Dr. Sara Clinic"], Seq = 1, AllergyType = 1, Description = "Penicillin", Status = 1, CreatedBy = adminPersonId });
        db.SaveChanges();

        // ---------- audit: seed login for admin ----------
        db.AuditLogs.Add(new AuditLog { PersonId = adminPersonId, EventType = "user_created", Detail = "Admin account seeded by DbSeeder." });
        db.LoginAttempts.Add(new LoginAttempt { PersonId = adminPersonId, Email = "admin@crm.local", IpAddress = "127.0.0.1", Success = true, CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }

    // ---------- helpers ----------

    private static void EnsureEwSet(AppDbContext db, string page, string pscode, string description)
    {
        var exists = db.EwSets.Any(s => s.Page == page && s.Pscode == pscode);
        if (!exists)
            db.EwSets.Add(new EwSet { Page = page, Pscode = pscode, Description = description, Status = 1 });
    }
}

// ---------- BCrypt helper ----------
public static class BCryptHasher
{
    public static string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public static bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
