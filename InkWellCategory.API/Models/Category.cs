using InkWell.Shared;

namespace InkWellCategory.API.Models;

public class Category : BaseEntity
{
    public Guid CategoryId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    // SEO-friendly URL slug like "web-development"
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    // if null, this is a root category & if it has a value, then it's a child category
    public Guid? ParentCategoryId { get; set; }

    // cached count for display
    public int PostCount { get; set; }
}