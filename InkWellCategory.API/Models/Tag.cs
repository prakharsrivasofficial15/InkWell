using InkWell.Shared;

namespace InkWellCategory.API.Models;

public class Tag : BaseEntity
{
    public Guid TagId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    // incremented when tag is assigned to a post
    public int PostCount { get; set; }
}