namespace Crm.Api.Models;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ContactPerson { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public ClientType Type { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Potential;
    public string? Notes { get; set; }
    public int CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Subscription> Subscriptions { get; set; } = [];
    public ICollection<Interaction> Interactions { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<FollowUp> FollowUps { get; set; } = [];
}
