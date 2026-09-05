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
    public static string ClientDisplayName(this Person p)
    {
        var parts = new[] { p.FirstName, p.MiddleName, p.LastName }.Where(x => !string.IsNullOrWhiteSpace(x));
        return string.Join(" ", parts);
    }

    public static string UserDisplayName(this PersonCredential c)
        => c.Person?.ClientDisplayName() ?? c.Username ?? "?";

    public static PlanDto ToDto(this SubscriptionPlan p) =>
        new(p.Id, p.Name, p.Cycle.ToString(), p.Price, p.IsActive);

    public static SubscriptionDto ToDto(this Subscription s) =>
        new(s.Id, s.ClientId, s.Client.ClientDisplayName(), s.Client.Phone, s.PlanId, s.Plan.Name,
            s.Cycle.ToString(), s.StartDate, s.ExpiryDate, s.Price, s.PaymentStatus.ToString(),
            s.PaymentMethod, s.PaidAt, s.LicenseKey, s.LicenseKeyIssuedAt, s.Notes, s.CreatedAt);

    /// <summary>A paid subscription as a payment-history row.</summary>
    public static PaymentDto ToPaymentDto(this Subscription s) =>
        new(s.Id, s.Plan.Name, s.Cycle.ToString(), s.StartDate, s.ExpiryDate,
            s.Price, s.PaymentMethod, s.PaidAt!.Value, s.LicenseKey);

    public static InteractionDto ToDto(this Interaction i) =>
        new(i.Id, i.ClientId, i.Client.ClientDisplayName(), i.Type.ToString(), i.Outcome.ToString(),
            i.Notes, i.NextFollowUpAt, i.UserId, i.User.UserDisplayName(), i.CreatedAt);

    public static FollowUpDto ToDto(this FollowUp f) =>
        new(f.Id, f.ClientId, f.Client.ClientDisplayName(), f.Title, f.Description,
            f.Type.ToString(), f.TicketId, f.Ticket?.Title,
            f.ScheduledAt, f.Status.ToString(), f.AssignedToId, f.AssignedTo.UserDisplayName(),
            f.ReminderSentAt, f.CreatedAt);

    public static ClientContactDto ToDto(this ClientContact c) =>
        new(c.Id, c.ClientId, c.Name, c.Phone, c.Email, c.Notes, c.AllowWhatsApp);

    public static TicketCommentDto ToDto(this TicketComment c) =>
        new(c.Id, c.UserId, c.User.UserDisplayName(), c.Body, c.IsInternal, c.CreatedAt);

    public static TicketDto ToDto(this Ticket t, int commentCount = 0) =>
        new(t.Id, t.ClientId, t.Client.ClientDisplayName(), t.Title, t.Description,
            t.Priority.ToString(), t.Status.ToString(), t.AssignedToId, t.AssignedTo?.UserDisplayName(),
            t.CreatedBy.UserDisplayName(), t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.ResolvedVersion, commentCount);
}
