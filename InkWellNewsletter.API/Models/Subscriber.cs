using InkWell.Shared;

namespace InkWellNewsletter.API.Models;

public enum SubscriberStatus { PENDING, ACTIVE, UNSUBSCRIBED }

public class Subscriber : BaseEntity
{
    public Guid SubscriberId { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    // nullable - non-registered visitors can also subscribe
    public Guid? UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public SubscriberStatus Status { get; set; } = SubscriberStatus.PENDING;

    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnsubscribedAt { get; set; }

    // unique GUID token used in confirmation + unsubscribe links
    public string Token { get; set; } = Guid.NewGuid().ToString();

    // comma-separated preference tags e.g. "tech,design,business"
    public string? Preferences { get; set; }

    // confirmation token expires after 24 hours
    public DateTime TokenExpiry { get; set; } = DateTime.UtcNow.AddHours(24);
}