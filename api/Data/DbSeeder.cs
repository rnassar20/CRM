using Crm.Api.Models;

namespace Crm.Api.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Users.Any()) return;

        var admin = new User
        {
            FullName = "Administrator",
            Email = "admin@crm.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = UserRole.Admin
        };
        db.Users.Add(admin);
        db.SaveChanges();

        var basicYearly = new SubscriptionPlan { Name = "Basic - 1 Year", Cycle = BillingCycle.Yearly, Price = 1000m };
        var proYearly = new SubscriptionPlan { Name = "Pro - 1 Year", Cycle = BillingCycle.Yearly, Price = 2000m };
        var premiumYearly = new SubscriptionPlan { Name = "Premium - 1 Year + Priority Support", Cycle = BillingCycle.Yearly, Price = 3500m };
        var basicMonthly = new SubscriptionPlan { Name = "Basic - Monthly", Cycle = BillingCycle.Monthly, Price = 120m };
        db.Plans.AddRange(basicYearly, proYearly, premiumYearly, basicMonthly);
        db.SaveChanges();

        var today = DateTime.Today;

        var pharmacy = new Client
        {
            Name = "Al-Shifa Pharmacy",
            ContactPerson = "Ahmed Hassan",
            Phone = "+201001234567",
            Email = "info@alshifa.example",
            City = "Cairo",
            Type = ClientType.Pharmacy,
            Status = ClientStatus.Subscribed,
            CreatedById = admin.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-400)
        };
        var gift = new Client
        {
            Name = "Sunshine Gifts",
            ContactPerson = "Mona Adel",
            Phone = "+201007654321",
            City = "Giza",
            Type = ClientType.GiftShop,
            Status = ClientStatus.Interested,
            CreatedById = admin.Id
        };
        var clinic = new Client
        {
            Name = "Dr. Sara Clinic",
            ContactPerson = "Dr. Sara Ibrahim",
            Phone = "+201112223333",
            City = "Cairo",
            Type = ClientType.DoctorClinic,
            Status = ClientStatus.Potential,
            CreatedById = admin.Id
        };
        db.Clients.AddRange(pharmacy, gift, clinic);
        db.SaveChanges();

        // secondary contact persons (opt-in WhatsApp fan-out demo)
        db.ClientContacts.AddRange(
            new ClientContact
            {
                Client = pharmacy,
                Name = "Khaled Mansour",
                Phone = "+201005554433",
                Email = "khaled@alshifa.example",
                Notes = "Pharmacist on duty, handles license renewals.",
                AllowWhatsApp = true
            },
            new ClientContact
            {
                Client = pharmacy,
                Name = "Nour El-Sayed",
                Phone = "+201009998877",
                Notes = "Accountant - payments only, no notifications.",
                AllowWhatsApp = false
            });
        db.SaveChanges();

        var sub = new Subscription
        {
            Client = pharmacy,
            Plan = basicYearly,
            Cycle = BillingCycle.Yearly,
            StartDate = today.AddDays(-358),
            ExpiryDate = today.AddDays(7),
            Price = basicYearly.Price,
            PaymentStatus = PaymentStatus.Unpaid
        };
        db.Subscriptions.Add(sub);

        db.Interactions.AddRange(
            new Interaction
            {
                Client = gift,
                Type = InteractionType.Call,
                Outcome = InteractionOutcome.Interested,
                Notes = "Asked for pricing sheet, wants a call back next week.",
                NextFollowUpAt = DateTime.Today.AddDays(3),
                UserId = admin.Id
            },
            new Interaction
            {
                Client = clinic,
                Type = InteractionType.WhatsApp,
                Outcome = InteractionOutcome.CallbackRequested,
                Notes = "Receptionist said the doctor reviews requests after 6 PM.",
                NextFollowUpAt = DateTime.Today.AddDays(1),
                UserId = admin.Id
            });

        db.FollowUps.AddRange(
            new FollowUp
            {
                Client = gift,
                Title = "Send pricing sheet & confirm interest",
                Type = FollowUpType.Marketing,
                ScheduledAt = DateTime.Today.AddDays(3).AddHours(11),
                AssignedToId = admin.Id,
                CreatedById = admin.Id
            },
            new FollowUp
            {
                Client = clinic,
                Title = "First contact call",
                Type = FollowUpType.Marketing,
                ScheduledAt = DateTime.Today.AddHours(2),
                AssignedToId = admin.Id,
                CreatedById = admin.Id
            });

        var ticket = new Ticket
        {
            Client = pharmacy,
            Title = "Barcode printer not printing invoices",
            Description = "Printer works in test page but not from invoice screen.",
            Priority = TicketPriority.High,
            Status = TicketStatus.Open,
            CreatedById = admin.Id
        };
        db.Tickets.Add(ticket);
        db.SaveChanges();

        db.FollowUps.Add(new FollowUp
        {
            Client = pharmacy,
            Title = "Check ticket fix with client",
            Description = $"Verify invoice printing after build v2.4.1 update (ticket #{ticket.Id}).",
            Type = FollowUpType.Support,
            TicketId = ticket.Id,
            ScheduledAt = DateTime.Today.AddDays(1).AddHours(14),
            AssignedToId = admin.Id,
            CreatedById = admin.Id
        });

        db.SaveChanges();
    }
}
