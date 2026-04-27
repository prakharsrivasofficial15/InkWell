using InkWellComment.API.Data;
using InkWellComment.API.Interfaces;
using InkWellComment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellComment.API.Repositories;

public class CommentRepository : ICommentRepository
{
    private readonly CommentDbContext _db;
    public CommentRepository(CommentDbContext db) => _db = db;

    public async Task<Comment?> GetByIdAsync(Guid id) =>
        await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == id);

    public async Task<Comment?> GetByCommentIdAsync(Guid commentId) =>
        await _db.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId);

    public async Task<IEnumerable<Comment>> GetAllAsync() =>
        await _db.Comments.ToListAsync();

    public async Task<Comment> AddAsync(Comment entity)
    {
        _db.Comments.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<Comment> UpdateAsync(Comment entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Comments.Update(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    // soft deletes: mark as deleted, but not removed from DB
    public async Task DeleteAsync(Guid id)
    {
        var comment = await GetByCommentIdAsync(id);
        if (comment is null) return;
        comment.IsDeleted = true;
        comment.Status = CommentStatus.DELETED;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _db.Comments.AnyAsync(c => c.CommentId == id);

    public async Task<IEnumerable<Comment>> GetByPostIdAsync(Guid postId) =>
        await _db.Comments
            .Where(c => c.PostId == postId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

    // only top-level comments
    public async Task<IEnumerable<Comment>> GetTopLevelByPostIdAsync(Guid postId) =>
        await _db.Comments
            .Where(c => c.PostId == postId && c.ParentCommentId == null)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Comment>> GetRepliesAsync(Guid parentCommentId) =>
        await _db.Comments
            .Where(c => c.ParentCommentId == parentCommentId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Comment>> GetByAuthorIdAsync(Guid authorId) =>
        await _db.Comments
            .Where(c => c.AuthorId == authorId)
            .ToListAsync();

    public async Task<IEnumerable<Comment>> GetByStatusAsync(CommentStatus status) =>
        await _db.Comments
            .Where(c => c.Status == status)
            .ToListAsync();

    public async Task<int> CountByPostIdAsync(Guid postId) =>
        await _db.Comments
            .CountAsync(c => c.PostId == postId &&
                            c.Status == CommentStatus.APPROVED);

    // deleting parent also soft-deletes all replies
    public async Task SoftDeleteWithRepliesAsync(Guid commentId)
    {
        var comment = await GetByCommentIdAsync(commentId);
        if (comment is null) return;

        // soft delete the parent
        comment.IsDeleted = true;
        comment.Status = CommentStatus.DELETED;

        // soft delete all replies
        var replies = await GetRepliesAsync(commentId);
        foreach (var reply in replies)
        {
            reply.IsDeleted = true;
            reply.Status = CommentStatus.DELETED;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<CommentLike?> GetLikeAsync(Guid commentId, Guid userId) =>
        await _db.CommentLikes
            .FirstOrDefaultAsync(cl => cl.CommentId == commentId
                                    && cl.UserId == userId);

    public async Task AddLikeAsync(CommentLike like)
    {
        _db.CommentLikes.Add(like);
        await _db.SaveChangesAsync();
    }

    public async Task RemoveLikeAsync(CommentLike like)
    {
        _db.CommentLikes.Remove(like);
        await _db.SaveChangesAsync();
    }
}