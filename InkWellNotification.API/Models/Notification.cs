using InkWell.Shared;

namespace InkWellNotification.API.Models;

public enum NotificationType
{
    NEW_COMMENT,      // author gets notified when someone comments on their post
    COMMENT_REPLY,    // commenter gets notified when someone replies
    MENTION,          // user gets notified when mentioned in a comment
    NEW_POST,         // reader gets notified when followed author publishes
    LIKE,             // author gets notified when post is liked
    BROADCAST         // admin sends to all users
}

public class Notification : BaseEntity
{
    public Guid NotificationId { get; set; } = Guid.NewGuid();

    // who receives this notification
    public Guid RecipientId { get; set; }

    // who triggered the event (null for system notifications)
    public Guid? ActorId { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // deep link reference like PostId or CommentId
    public Guid? RelatedId { get; set; }

    // like "Post", "Comment"
    public string? RelatedType { get; set; }

    public bool IsRead { get; set; } = false;
}