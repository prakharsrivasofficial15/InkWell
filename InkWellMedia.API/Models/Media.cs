using InkWell.Shared;

namespace InkWellMedia.API.Models;

public class Media : BaseEntity
{
    public Guid MediaId { get; set; } = Guid.NewGuid();

    // who uploaded this file
    public Guid UploaderId { get; set; }

    // original filename from user's device
    public string OriginalName { get; set; } = string.Empty;

    // stored filename in blob storage (GUID-based to avoid conflicts)
    public string Filename { get; set; } = string.Empty;

    // full Azure Blob Storage URL
    public string Url { get; set; } = string.Empty;

    // e.g. image/jpeg, image/png, application/pdf
    public string MimeType { get; set; } = string.Empty;

    // file size in kilobytes
    public long SizeKb { get; set; }

    // accessibility text for images
    public string? AltText { get; set; }

    // which post this media is linked
    public Guid? LinkedPostId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}