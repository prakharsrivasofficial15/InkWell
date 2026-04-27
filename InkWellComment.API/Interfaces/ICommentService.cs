using InkWellComment.API.DTOs;

namespace InkWellComment.API.Interfaces;

public interface ICommentService
{
    Task<CommentResponse> AddCommentAsync(Guid authorId, AddCommentRequest request);
    Task<CommentResponse> GetCommentByIdAsync(Guid commentId);
    Task<IEnumerable<CommentResponse>> GetCommentsByPostAsync(Guid postId);
    Task<IEnumerable<CommentResponse>> GetRepliesAsync(Guid parentCommentId);
    Task<CommentResponse> UpdateCommentAsync(Guid commentId, Guid authorId, UpdateCommentRequest request);
    Task DeleteCommentAsync(Guid commentId, Guid authorId);
    Task<CommentResponse> ApproveCommentAsync(Guid commentId);
    Task<CommentResponse> RejectCommentAsync(Guid commentId);
    Task LikeCommentAsync(Guid commentId, Guid userId);
    Task UnlikeCommentAsync(Guid commentId, Guid userId);
    Task<int> GetCommentCountAsync(Guid postId);
}