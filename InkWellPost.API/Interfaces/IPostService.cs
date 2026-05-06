using InkWellPost.API.DTOs;

namespace InkWellPost.API.Interfaces;

public interface IPostService
{
    Task<PostResponse> CreatePostAsync(Guid authorId, CreatePostRequest request);
    Task<PostResponse> GetPostByIdAsync(Guid postId);
    Task<PostResponse> GetPostBySlugAsync(string slug);
    Task<IEnumerable<PostSummaryResponse>> GetPostsByAuthorAsync(Guid authorId);
    Task<IEnumerable<PostSummaryResponse>> GetPublishedPostsAsync();
    Task<IEnumerable<PostSummaryResponse>> SearchPostsAsync(string keyword);
    Task<PostResponse> UpdatePostAsync(Guid postId, Guid authorId, UpdatePostRequest request);
    Task<PostResponse> PublishPostAsync(Guid postId, Guid authorId, string userRole);
    Task<PostResponse> UnpublishPostAsync(Guid postId, Guid authorId, string userRole);
    Task DeletePostAsync(Guid postId, Guid authorId, string userRole);
    Task IncrementViewsAsync(Guid postId, string sessionId);
    Task LikePostAsync(Guid postId, Guid userId);
    Task UnlikePostAsync(Guid postId, Guid userId);
    Task<PostStatsResponse> GetPostStatsAsync(Guid postId);
    Task<int> GetPostCountAsync(Guid authorId);
}