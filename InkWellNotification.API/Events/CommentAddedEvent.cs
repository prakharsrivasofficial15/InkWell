namespace InkWellNotification.API.Events;

// received from Azure Service Bus when comment-service adds a comment
public record CommentAddedEvent(
    Guid CommentId,
    Guid PostId,
    Guid AuthorId,         // who wrote the comment
    Guid PostAuthorId,     // who wrote the post (for new comment notifications)
    Guid? ParentCommentId, // if reply then notify the parent commenter
    Guid? ParentCommentAuthorId, // who wrote the parent comment (for reply notifications)
    string Content,
    DateTime CreatedAt
);