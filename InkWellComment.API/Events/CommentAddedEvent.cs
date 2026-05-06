namespace InkWellComment.API.Events;

// Fires when someone adds a comment - gets sent to Azure Service Bus. The notification service listens for this to let the post author know
public record CommentAddedEvent(
    Guid CommentId,
    Guid PostId,
    Guid AuthorId,
    Guid PostAuthorId,     // who wrote the post (for new comment notifications)
    Guid? ParentCommentId,  // if this is a reply, we'll notify the parent commenter
    Guid? ParentCommentAuthorId, // who wrote the parent comment (for reply notifications)
    string Content,
    DateTime CreatedAt
);