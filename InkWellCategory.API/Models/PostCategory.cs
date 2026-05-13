using InkWell.Shared;

namespace InkWellCategory.API.Models;

// join table linking posts to categories (many-to-many)
public class PostCategory : BaseEntity
{
    public Guid PostCategoryId { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid CategoryId { get; set; }
}