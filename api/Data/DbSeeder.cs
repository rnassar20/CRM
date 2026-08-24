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

        var basic = new SubscriptionPlan { Name = "Basic - 1 Year", DurationDays = 365, Price = 1000m };
        var pro = new SubscriptionPlan { Name = "Pro - 1 Year", DurationDays = 365, Price = 2000m };
        var premium = new SubscriptionPlan { Name = "Premium - 1 Year + Priority Support", DurationDays = 365, Price = 3500m };
        db.Plans.AddRange(basic, pro, premium);
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

        var sub = new Subscription
        {
            Client = pharmacy,
            Plan = basic,
            StartDate = today.AddDays(-358),
            ExpiryDate = today.AddDays(7),
            Price = basic.Price,
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
                ScheduledAt = DateTime.Today.AddDays(3).AddHours(11),
                AssignedToId = admin.Id,
                CreatedById = admin.Id
            },
            new FollowUp
            {
                Client = clinic,
                Title = "First contact call",
                ScheduledAt = DateTime.Today.AddHours(2),
                AssignedToId = admin.Id,
                CreatedById = admin.Id
            });

        db.Tickets.Add(new Ticket
        {
            Client = pharmacy,
            Title = "Barcode printer not printing invoices",
            Description = "Printer works in test page but not from invoice screen.",
            Priority = TicketPriority.High,
            Status = TicketStatus.Open,
            CreatedById = admin.Id
        });

        db.SaveChanges();
    }
}
