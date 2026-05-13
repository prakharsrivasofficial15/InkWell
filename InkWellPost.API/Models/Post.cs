using InkWell.Shared;

namespace InkWellPost.API.Models;

public enum PostStatus { DRAFT, PUBLISHED, UNPUBLISHED, ARCHIVED }

public class Post : BaseEntity
{
    public Guid PostId { get; set; } = Guid.NewGuid();
    public Guid AuthorId { get; set; }

    public string Title { get; set; } = string.Empty;

    // auto-generated from title using Slugify
    public string Slug { get; set; } = string.Empty;

    // rich HTML content - sanitized before save
    public string Content { get; set; } = string.Empty;

    // short summary shown in feed listings
    public string? Excerpt { get; set; }

    // URL to blob storage image
    public string? FeaturedImageUrl { get; set; }

    public PostStatus Status { get; set; } = PostStatus.DRAFT;

    // computed server-side: wordCount / 200
    public int ReadTimeMin { get; set; }

    // automatic increment atomically per unique session
    public int ViewCount { get; set; }

    // denormalized for fast feed rendering
    public int LikesCount { get; set; }

    public DateTime? PublishedAt { get; set; }
}