namespace InkWellNotification.API.Events;

// received from Azure Service Bus when comment-service adds a comment
public record CommentAddedEvent(
    Guid CommentId,
    Guid PostId,
    Guid AuthorId,         // who wrote the comment
    Guid? ParentCommentId, // if reply then notify the parent commenter
    string Content,
    DateTime CreatedAt
);