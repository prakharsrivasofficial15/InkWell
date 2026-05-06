namespace InkWellComment.API.DTOs;

// Requests

public record AddCommentRequest(
    Guid PostId,
    Guid PostAuthorId,        // who owns the post - for proper notification routing
    string Content,
    Guid? ParentCommentId  // null = top-level, Guid = reply
);

public record UpdateCommentRequest(
    string Content
);

// Responses

public record CommentResponse(
    Guid CommentId,
    Guid PostId,
    Guid AuthorId,
    Guid? ParentCommentId,
    string Content,
    int LikesCount,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<CommentResponse>? Replies
);

public record CommentCountResponse(
    Guid PostId,
    int TotalComments
);