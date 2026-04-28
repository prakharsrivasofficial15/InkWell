using InkWellMedia.API.DTOs;
using Microsoft.AspNetCore.Http;

namespace InkWellMedia.API.Interfaces;

public interface IMediaService
{
    Task<MediaResponse> UploadMediaAsync(Guid uploaderId, IFormFile file);
    Task<MediaResponse> GetMediaByIdAsync(Guid mediaId);
    Task<IEnumerable<MediaResponse>> GetMediaByUploaderAsync(Guid uploaderId);
    Task<IEnumerable<MediaResponse>> GetMediaByPostAsync(Guid postId);
    Task<IEnumerable<MediaResponse>> GetAllMediaAsync();
    Task<MediaResponse> UpdateAltTextAsync(Guid mediaId, Guid uploaderId, string altText);
    Task DeleteMediaAsync(Guid mediaId, Guid uploaderId);
    Task<MediaResponse> LinkToPostAsync(Guid mediaId, Guid postId);
    Task<MediaResponse> UnlinkFromPostAsync(Guid mediaId);
    Task CleanupDeletedAsync();
}