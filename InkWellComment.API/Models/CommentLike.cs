using InkWell.Shared;

namespace InkWellComment.API.Models;

// tracks who likes which comment & prevents duplicacy
public class CommentLike : BaseEntity
{
    public Guid CommentLikeId { get; set; } = Guid.NewGuid();
    public Guid CommentId { get; set; }
    public Guid UserId { get; set; }
}