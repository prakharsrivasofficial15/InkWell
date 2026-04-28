using InkWell.Shared;
using InkWellMedia.API.Models;

namespace InkWellMedia.API.Interfaces;

public interface IMediaRepository : IBaseRepository<Media>
{
    Task<Media?> GetByMediaIdAsync(Guid mediaId);
    Task<IEnumerable<Media>> GetByUploaderIdAsync(Guid uploaderId);
    Task<IEnumerable<Media>> GetByLinkedPostIdAsync(Guid postId);
    Task<IEnumerable<Media>> GetByMimeTypeAsync(string mimeType);
    Task<IEnumerable<Media>> GetAllMediaAsync();
    Task<IEnumerable<Media>> GetDeletedMediaAsync();
    Task<int> CountByUploaderIdAsync(Guid uploaderId);
}