namespace InkWellPost.API.Events;

// received from Azure Service Bus when post-service publishes a post
public record PostPublishedEvent(
    Guid PostId,
    Guid AuthorId,
    string Title,
    string Slug,
    string Excerpt,
    DateTime PublishedAt
);
