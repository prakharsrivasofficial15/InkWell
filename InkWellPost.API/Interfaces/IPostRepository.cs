using InkWell.Shared;
using InkWellPost.API.Models;

namespace InkWellPost.API.Interfaces;

public interface IPostRepository : IBaseRepository<Post>
{
    Task<Post?> GetBySlugAsync(string slug);
    Task<Post?> GetByPostIdAsync(Guid postId);
    Task<IEnumerable<Post>> GetByAuthorIdAsync(Guid authorId);
    Task<IEnumerable<Post>> GetByStatusAsync(PostStatus status);
    Task<IEnumerable<Post>> GetPublishedOrderedAsync();
    Task<IEnumerable<Post>> SearchByTitleAsync(string keyword);
    Task<IEnumerable<Post>> SearchByKeywordAsync(string keyword);
    Task<bool> SlugExistsAsync(string slug);
    Task<int> CountByAuthorIdAsync(Guid authorId);

    //tracking likes
    Task<PostLike?> GetLikeAsync(Guid postId, Guid userId);
    Task AddLikeAsync(PostLike like);
    Task RemoveLikeAsync(PostLike like);
    
    // atomic likes count updates
    Task IncrementLikesCountAsync(Guid postId);
    Task DecrementLikesCountAsync(Guid postId);
}