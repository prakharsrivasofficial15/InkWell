namespace InkWellNotification.API.Events;

public record PostPublishedEvent(
    Guid PostId,
    Guid AuthorId,
    string Title,
    string Slug,
    string? Excerpt,
    DateTime PublishedAt
);