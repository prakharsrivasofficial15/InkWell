using InkWellMedia.API.Data;
using InkWellMedia.API.Interfaces;
using InkWellMedia.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellMedia.API.Repositories;

public class MediaRepository : IMediaRepository
{
    private readonly MediaDbContext _db;
    public MediaRepository(MediaDbContext db) => _db = db;

    public async Task<Media?> GetByIdAsync(Guid id) =>
        await _db.MediaFiles.FirstOrDefaultAsync(m => m.MediaId == id);

    public async Task<Media?> GetByMediaIdAsync(Guid mediaId) =>
        await _db.MediaFiles.FirstOrDefaultAsync(m => m.MediaId == mediaId);

    public async Task<IEnumerable<Media>> GetAllAsync() =>
        await _db.MediaFiles.ToListAsync();

    public async Task<IEnumerable<Media>> GetAllMediaAsync() =>
        await _db.MediaFiles
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();

    public async Task<Media> AddAsync(Media entity)
    {
        _db.MediaFiles.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<Media> UpdateAsync(Media entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _db.MediaFiles.Update(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    // soft deletes, it preserves the referential integrity in post content
    public async Task DeleteAsync(Guid id)
    {
        var media = await GetByMediaIdAsync(id);
        if (media is null) return;
        media.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _db.MediaFiles.AnyAsync(m => m.MediaId == id);

    public async Task<IEnumerable<Media>> GetByUploaderIdAsync(Guid uploaderId) =>
        await _db.MediaFiles
            .Where(m => m.UploaderId == uploaderId)
            .OrderByDescending(m => m.UploadedAt)
            .ToListAsync();

    public async Task<IEnumerable<Media>> GetByLinkedPostIdAsync(Guid postId) =>
        await _db.MediaFiles
            .Where(m => m.LinkedPostId == postId)
            .ToListAsync();

    public async Task<IEnumerable<Media>> GetByMimeTypeAsync(string mimeType) =>
        await _db.MediaFiles
            .Where(m => m.MimeType == mimeType)
            .ToListAsync();

    // bypass soft delete filter to find deleted records for cleanup
    public async Task<IEnumerable<Media>> GetDeletedMediaAsync() =>
        await _db.MediaFiles
            .IgnoreQueryFilters()
            .Where(m => m.IsDeleted)
            .ToListAsync();

    public async Task<int> CountByUploaderIdAsync(Guid uploaderId) =>
        await _db.MediaFiles.CountAsync(m => m.UploaderId == uploaderId);

    // hard delete - permanently removes from database
    public async Task HardDeleteAsync(Guid mediaId)
    {
        var media = await _db.MediaFiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.MediaId == mediaId);
        
        if (media is not null)
        {
            _db.MediaFiles.Remove(media);
            await _db.SaveChangesAsync();
        }
    }
}