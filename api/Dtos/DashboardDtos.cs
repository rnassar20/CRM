using Crm.Api.Models;

namespace Crm.Api.Dtos;

public record DashboardStatsDto(
    int ClientsTotal,
    Dictionary<string, int> ClientsByStatus,
    Dictionary<string, int> ClientsByType,
    int SubscriptionsActive,
    int SubscriptionsExpiringIn30,
    int SubscriptionsExpired,
    int SubscriptionsUnpaidActive,
    Dictionary<string, int> TicketsByStatus,
    int TicketsOpen,
    int FollowUpsToday,
    int FollowUpsOverdue,
    int WhatsAppSentLast30Days,
    IReadOnlyList<FollowUpDto> UpcomingFollowUps,
    IReadOnlyList<InteractionDto> RecentInteractions);

public record LicenseCheckResponse(bool Valid, string? Error, int? ClientId, int? SubscriptionId, DateTime? ExpiryDate, bool MatchesSubscription);

public static class Mappers
{
    public static PlanDto ToDto(this SubscriptionPlan p) =>
        new(p.Id, p.Name, p.DurationDays, p.Price, p.IsActive);

    public static SubscriptionDto ToDto(this Subscription s) =>
        new(s.Id, s.ClientId, s.Client.Name, s.Client.Phone, s.PlanId, s.Plan.Name,
            s.StartDate, s.ExpiryDate, s.Price, s.PaymentStatus.ToString(),
            s.PaymentMethod, s.PaidAt, s.LicenseKey, s.LicenseKeyIssuedAt, s.Notes, s.CreatedAt);

    public static InteractionDto ToDto(this Interaction i) =>
        new(i.Id, i.ClientId, i.Client.Name, i.Type.ToString(), i.Outcome.ToString(),
            i.Notes, i.NextFollowUpAt, i.UserId, i.User.FullName, i.CreatedAt);

    public static FollowUpDto ToDto(this FollowUp f) =>
        new(f.Id, f.ClientId, f.Client.Name, f.Title, f.Description,
            f.ScheduledAt, f.Status.ToString(), f.AssignedToId, f.AssignedTo.FullName,
            f.ReminderSentAt, f.CreatedAt);

    public static TicketCommentDto ToDto(this TicketComment c) =>
        new(c.Id, c.UserId, c.User.FullName, c.Body, c.IsInternal, c.CreatedAt);

    public static TicketDto ToDto(this Ticket t, int commentCount = 0) =>
        new(t.Id, t.ClientId, t.Client.Name, t.Title, t.Description,
            t.Priority.ToString(), t.Status.ToString(), t.AssignedToId, t.AssignedTo?.FullName,
            t.CreatedBy.FullName, t.CreatedAt, t.UpdatedAt, t.ResolvedAt, commentCount);
}
