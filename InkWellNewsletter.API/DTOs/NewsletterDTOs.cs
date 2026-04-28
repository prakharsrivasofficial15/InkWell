namespace InkWellNewsletter.API.DTOs;

// ── Requests ──────────────────────────────────────────────────────────────────

public record SubscribeRequest(
    string Email,
    string FullName,
    Guid? UserId,
    string? Preferences
);

public record SendNewsletterRequest(
    string Subject,
    string HtmlContent,
    string? FilterByPreference  // null = send to all ACTIVE subscribers
);

public record UpdatePreferencesRequest(
    string Preferences
);

// ── Responses ─────────────────────────────────────────────────────────────────

public record SubscriberResponse(
    Guid SubscriberId,
    string Email,
    string FullName,
    string Status,
    string? Preferences,
    DateTime SubscribedAt,
    DateTime? UnsubscribedAt
);

public record CampaignResponse(
    Guid CampaignId,
    string Subject,
    string Status,
    int RecipientCount,
    DateTime? SentAt,
    DateTime CreatedAt
);

public record SubscriberCountResponse(
    int Total,
    int Active,
    int Pending,
    int Unsubscribed
);