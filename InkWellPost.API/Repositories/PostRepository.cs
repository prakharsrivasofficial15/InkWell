using InkWellPost.API.Data;
using InkWellPost.API.Interfaces;
using InkWellPost.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellPost.API.Repositories;

public class PostRepository : IPostRepository
{
    private readonly PostDbContext _db;
    public PostRepository(PostDbContext db) => _db = db;

    public async Task<Post?> GetByIdAsync(Guid id) =>
        await _db.Posts.FirstOrDefaultAsync(p => p.PostId == id);

    public async Task<Post?> GetByPostIdAsync(Guid postId) =>
        await _db.Posts.FirstOrDefaultAsync(p => p.PostId == postId);

    public async Task<Post?> GetBySlugAsync(string slug) =>
        await _db.Posts.FirstOrDefaultAsync(p => p.Slug == slug);

    public async Task<IEnumerable<Post>> GetAllAsync() =>
        await _db.Posts.ToListAsync();

    public async Task<Post> AddAsync(Post entity)
    {
        _db.Posts.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<Post> UpdateAsync(Post entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Posts.Update(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(Guid id)
    {
        var post = await GetByIdAsync(id);
        if (post is null) return;
        post.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _db.Posts.AnyAsync(p => p.PostId == id);

    public async Task<IEnumerable<Post>> GetByAuthorIdAsync(Guid authorId) =>
        await _db.Posts
            .Where(p => p.AuthorId == authorId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Post>> GetByStatusAsync(PostStatus status) =>
        await _db.Posts.Where(p => p.Status == status).ToListAsync();

    public async Task<IEnumerable<Post>> GetPublishedOrderedAsync() =>
        await _db.Posts
            .Where(p => p.Status == PostStatus.PUBLISHED)
            .OrderByDescending(p => p.PublishedAt)
            .ToListAsync();

    public async Task<IEnumerable<Post>> SearchByTitleAsync(string keyword) =>
        await _db.Posts
            .Where(p => p.Title.Contains(keyword) && p.Status == PostStatus.PUBLISHED)
            .ToListAsync();

    public async Task<IEnumerable<Post>> SearchByKeywordAsync(string keyword) =>
        await _db.Posts
            .Where(p => p.Status == PostStatus.PUBLISHED &&
                       (p.Title.Contains(keyword) || p.Content.Contains(keyword)))
            .ToListAsync();

    public async Task<bool> SlugExistsAsync(string slug) =>
        await _db.Posts.AnyAsync(p => p.Slug == slug);

    public async Task<int> CountByAuthorIdAsync(Guid authorId) =>
        await _db.Posts.CountAsync(p => p.AuthorId == authorId);

    public async Task<PostLike?> GetLikeAsync(Guid postId, Guid userId) =>
        await _db.PostLikes
            .FirstOrDefaultAsync(pl => pl.PostId == postId && pl.UserId == userId);

    public async Task AddLikeAsync(PostLike like)
    {
        _db.PostLikes.Add(like);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveLikeAsync(PostLike like)
    {
        _db.PostLikes.Remove(like);
        await _db.SaveChangesAsync();
    }
}