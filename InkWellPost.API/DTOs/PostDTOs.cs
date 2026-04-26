namespace InkWellPost.API.DTOs;

// ── Requests ─────────────────────────────────────────────────────────────────

public record CreatePostRequest(
    string Title,
    string Content,
    string? Excerpt,
    string? FeaturedImageUrl
);

public record UpdatePostRequest(
    string Title,
    string Content,
    string? Excerpt,
    string? FeaturedImageUrl
);

// ── Responses ─────────────────────────────────────────────────────────────────

public record PostResponse(
    Guid PostId,
    Guid AuthorId,
    string Title,
    string Slug,
    string Content,
    string? Excerpt,
    string? FeaturedImageUrl,
    string Status,
    int ReadTimeMin,
    int ViewCount,
    int LikesCount,
    DateTime CreatedAt,
    DateTime? PublishedAt
);

public record PostSummaryResponse(
    Guid PostId,
    Guid AuthorId,
    string Title,
    string Slug,
    string? Excerpt,
    string? FeaturedImageUrl,
    string Status,
    int ReadTimeMin,
    int ViewCount,
    int LikesCount,
    DateTime? PublishedAt
);

public record PostStatsResponse(
    Guid PostId,
    string Title,
    int ViewCount,
    int LikesCount
);
