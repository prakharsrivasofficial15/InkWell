namespace InkWellComment.API.DTOs;

// Requests

public record AddCommentRequest(
    Guid PostId,
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