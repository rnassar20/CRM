using Crm.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Crm.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
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

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.FullName).HasMaxLength(200);
            e.Property(u => u.Email).HasMaxLength(320);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        mb.Entity<Client>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.ContactPerson).HasMaxLength(200);
            e.Property(c => c.Phone).HasMaxLength(30);
            e.Property(c => c.Email).HasMaxLength(320);
            e.Property(c => c.City).HasMaxLength(100);
            e.Property(c => c.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(c => c.Status).HasConversion<string>().HasMaxLength(30);
            e.HasOne(c => c.CreatedBy).WithMany().HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(c => c.Status);
            e.HasIndex(c => c.Type);
        });

        mb.Entity<SubscriptionPlan>(e =>
        {
            e.ToTable("SubscriptionPlans");
            e.Property(p => p.Name).HasMaxLength(150);
            e.Property(p => p.Cycle).HasConversion<string>().HasMaxLength(10);
            e.Property(p => p.Price).HasColumnType("numeric(12,2)");
        });

        mb.Entity<Subscription>(e =>
        {
            e.Property(s => s.Price).HasColumnType("numeric(12,2)");
            e.Property(s => s.PaymentStatus).HasConversion<string>().HasMaxLength(10);
            e.Property(s => s.Cycle).HasConversion<string>().HasMaxLength(10);
            e.Property(s => s.PaymentMethod).HasMaxLength(50);
            e.HasIndex(s => s.ExpiryDate);
            e.HasOne(s => s.Client).WithMany(c => c.Subscriptions).HasForeignKey(s => s.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Plan).WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Ticket>(e =>
        {
            e.Property(t => t.Priority).HasConversion<string>().HasMaxLength(15);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(15);
            e.HasIndex(t => t.Status);
            e.HasOne(t => t.Client).WithMany(c => c.Tickets).HasForeignKey(t => t.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.AssignedTo).WithMany().HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<TicketComment>(e =>
        {
            e.HasOne(c => c.Ticket).WithMany(t => t.Comments).HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Interaction>(e =>
        {
            e.Property(i => i.Type).HasConversion<string>().HasMaxLength(15);
            e.Property(i => i.Outcome).HasConversion<string>().HasMaxLength(25);
            e.HasOne(i => i.Client).WithMany(c => c.Interactions).HasForeignKey(i => i.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.User).WithMany().HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<FollowUp>(e =>
        {
            e.Property(f => f.Status).HasConversion<string>().HasMaxLength(15);
            e.Property(f => f.Type).HasConversion<string>().HasMaxLength(12);
            e.HasIndex(f => f.ScheduledAt);
            e.HasOne(f => f.Client).WithMany(c => c.FollowUps).HasForeignKey(f => f.ClientId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.AssignedTo).WithMany().HasForeignKey(f => f.AssignedToId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.SourceInteraction).WithMany().HasForeignKey(f => f.SourceInteractionId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(f => f.Ticket).WithMany().HasForeignKey(f => f.TicketId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<ClientContact>(e =>
        {
            e.ToTable("ClientContacts");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(320);
            e.HasIndex(x => x.ClientId);
            e.HasOne(x => x.Client).WithMany(c => c.Contacts).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<WhatsAppMessage>(e =>
        {
            e.Property(w => w.MediaType).HasConversion<string>().HasMaxLength(15);
            e.Property(w => w.Direction).HasConversion<string>().HasMaxLength(10);
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(w => new { w.SubscriptionId, w.RelatedTag });
            e.HasOne(w => w.Client).WithMany().HasForeignKey(w => w.ClientId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(w => w.Subscription).WithMany().HasForeignKey(w => w.SubscriptionId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
