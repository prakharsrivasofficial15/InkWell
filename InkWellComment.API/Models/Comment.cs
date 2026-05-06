using InkWell.Shared;

namespace InkWellComment.API.Models;

public enum CommentStatus { APPROVED, PENDING, REJECTED, DELETED }

public class Comment : BaseEntity
{
    public Guid CommentId { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public Guid PostAuthorId { get; set; }    // who owns the post - for proper notification routing
    public Guid AuthorId { get; set; }

    // if it's null, then this is a top-level comment & if it has a value, then it's a reply to another comment
    public Guid? ParentCommentId { get; set; }

    public string Content { get; set; } = string.Empty;

    public int LikesCount { get; set; }

    // starts as APPROVED, but admin can change the platform to PENDING mode
    public CommentStatus Status { get; set; } = CommentStatus.APPROVED;
}