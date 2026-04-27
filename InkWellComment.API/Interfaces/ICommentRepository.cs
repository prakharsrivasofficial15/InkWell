using InkWell.Shared;
using InkWellComment.API.Models;

namespace InkWellComment.API.Interfaces;

public interface ICommentRepository : IBaseRepository<Comment>
{
    Task<Comment?> GetByCommentIdAsync(Guid commentId);
    Task<IEnumerable<Comment>> GetByPostIdAsync(Guid postId);
    Task<IEnumerable<Comment>> GetTopLevelByPostIdAsync(Guid postId);
    Task<IEnumerable<Comment>> GetRepliesAsync(Guid parentCommentId);
    Task<IEnumerable<Comment>> GetByAuthorIdAsync(Guid authorId);
    Task<IEnumerable<Comment>> GetByStatusAsync(CommentStatus status);
    Task<int> CountByPostIdAsync(Guid postId);
    Task SoftDeleteWithRepliesAsync(Guid commentId);

    // like tracking
    Task<CommentLike?> GetLikeAsync(Guid commentId, Guid userId);
    Task AddLikeAsync(CommentLike like);
    Task RemoveLikeAsync(CommentLike like);
}