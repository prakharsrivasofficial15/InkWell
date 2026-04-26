using InkWell.Shared;

namespace InkWellPost.API.Models;

// tracks which user liked which post & prevents duplicate likes
public class PostLike : BaseEntity
{
    public Guid PostLikeId { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid UserId { get; set; }
}