namespace InkWellNotification.API.DTOs;

// Requests

public record SendNotificationRequest(
    Guid RecipientId,
    Guid? ActorId,
    string Type,
    string Title,
    string Message,
    Guid? RelatedId,
    string? RelatedType
);

public record SendBulkNotificationRequest(
    List<Guid> RecipientIds,
    string Type,
    string Title,
    string Message,
    Guid? RelatedId,
    string? RelatedType
);

public record BroadcastNotificationRequest(
    string Title,
    string Message,
    string? TargetRole  // null = all users, "READER"/"AUTHOR"/"ADMIN" = specific role
);

// Responses

public record NotificationResponse(
    Guid NotificationId,
    Guid RecipientId,
    Guid? ActorId,
    string Type,
    string Title,
    string Message,
    Guid? RelatedId,
    string? RelatedType,
    bool IsRead,
    DateTime CreatedAt
);

public record UnreadCountResponse(
    Guid RecipientId,
    int UnreadCount
);