using InkWell.Shared;

namespace InkWellCategory.API.Models;

// join table linking posts to tags (many-to-many)
public class PostTag : BaseEntity
{
    public Guid PostTagId { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid TagId { get; set; }
}