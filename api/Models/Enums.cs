namespace Crm.Api.Models;

public enum UserRole { Admin, Agent }

public enum ClientType
{
    Pharmacy,
    GiftShop,
    DoctorClinic,
    Hospital,
    Other
}

public enum ClientStatus
{
    Potential,
    Contacted,
    Interested,
    NotInterested,
    Subscribed
}

public enum PaymentStatus { Unpaid, Paid }

public enum BillingCycle { Monthly, Yearly }

public enum TicketPriority { Low, Medium, High, Critical }

public enum TicketStatus { Open, InProgress, Resolved, Closed }

public enum InteractionType { Call, WhatsApp, Email, Visit, Sms }

public enum InteractionOutcome
{
    NoAnswer,
    CallbackRequested,
    Interested,
    NotInterested,
    DealClosed,
    InfoOnly
}

public enum FollowUpStatus { Pending, Done, Missed, Cancelled }

/// <summary>Internal = dev/build tasks, Support = tied to a ticket, Marketing = sales/outreach.</summary>
public enum FollowUpType { Marketing, Internal, Support }

public enum WhatsAppDirection { Outgoing, Incoming }

public enum WhatsAppMediaType { Text, Image, Voice, Document }

public enum WhatsAppStatus { Queued, Sent, Delivered, Failed }
