using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using InkWellMedia.API.DTOs;
using InkWellMedia.API.Interfaces;
using InkWellMedia.API.Models;
using Microsoft.AspNetCore.Http;

namespace InkWellMedia.API.Services;

public class MediaServiceImpl : IMediaService
{
    private readonly IMediaRepository _mediaRepo;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName = "inkwell-media";

    // allowed file types for security
    private static readonly HashSet<string> AllowedMimeTypes = new()
    {
        "image/jpeg", "image/png", "image/gif",
        "image/webp", "application/pdf"
    };

    // max 10MB per file
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public MediaServiceImpl(
        IMediaRepository mediaRepo,
        BlobServiceClient blobServiceClient)
    {
        _mediaRepo = mediaRepo;
        _blobServiceClient = blobServiceClient;
    }

    public async Task<MediaResponse> UploadMediaAsync(Guid uploaderId, IFormFile file)
    {
        // validate file size
        if (file.Length > MaxFileSizeBytes)
            throw new InvalidOperationException("File exceeds 10MB limit.");

        // validate mime type
        if (!AllowedMimeTypes.Contains(file.ContentType))
            throw new InvalidOperationException(
                "Invalid file type. Allowed: JPEG, PNG, GIF, WebP, PDF.");

        // generate unique blob filename
        var extension = Path.GetExtension(file.FileName);
        var blobName = $"{Guid.NewGuid()}{extension}";

        // upload to Azure Blob Storage
        var containerClient = _blobServiceClient
            .GetBlobContainerClient(_containerName);

        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobClient = containerClient.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, new BlobHttpHeaders
        {
            ContentType = file.ContentType
        });

        var blobUrl = blobClient.Uri.ToString();
        var sizeKb = file.Length / 1024;

        var media = new Media
        {
            UploaderId   = uploaderId,
            OriginalName = file.FileName,
            Filename     = blobName,
            Url          = blobUrl,
            MimeType     = file.ContentType,
            SizeKb       = sizeKb,
            UploadedAt   = DateTime.UtcNow
        };

        await _mediaRepo.AddAsync(media);
        return ToMediaResponse(media);
    }

    public async Task<MediaResponse> GetMediaByIdAsync(Guid mediaId)
    {
        var media = await _mediaRepo.GetByMediaIdAsync(mediaId)
            ?? throw new KeyNotFoundException("Media not found.");
        return ToMediaResponse(media);
    }

    public async Task<IEnumerable<MediaResponse>> GetMediaByUploaderAsync(Guid uploaderId)
    {
        var media = await _mediaRepo.GetByUploaderIdAsync(uploaderId);
        return media.Select(ToMediaResponse);
    }

    public async Task<IEnumerable<MediaResponse>> GetMediaByPostAsync(Guid postId)
    {
        var media = await _mediaRepo.GetByLinkedPostIdAsync(postId);
        return media.Select(ToMediaResponse);
    }

    public async Task<IEnumerable<MediaResponse>> GetAllMediaAsync()
    {
        var media = await _mediaRepo.GetAllMediaAsync();
        return media.Select(ToMediaResponse);
    }

    public async Task<MediaResponse> UpdateAltTextAsync(
        Guid mediaId, Guid uploaderId, string altText)
    {
        var media = await GetOwnedMediaAsync(mediaId, uploaderId);
        media.AltText = altText;
        await _mediaRepo.UpdateAsync(media);
        return ToMediaResponse(media);
    }

    public async Task DeleteMediaAsync(Guid mediaId, Guid uploaderId)
    {
        var media = await GetOwnedMediaAsync(mediaId, uploaderId);

        // delete from Azure Blob Storage
        var containerClient = _blobServiceClient
            .GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(media.Filename);
        await blobClient.DeleteIfExistsAsync();

        // soft delete in DB
        await _mediaRepo.DeleteAsync(mediaId);
    }

    public async Task<MediaResponse> LinkToPostAsync(Guid mediaId, Guid postId)
    {
        var media = await _mediaRepo.GetByMediaIdAsync(mediaId)
            ?? throw new KeyNotFoundException("Media not found.");

        media.LinkedPostId = postId;
        await _mediaRepo.UpdateAsync(media);
        return ToMediaResponse(media);
    }

    public async Task<MediaResponse> UnlinkFromPostAsync(Guid mediaId)
    {
        var media = await _mediaRepo.GetByMediaIdAsync(mediaId)
            ?? throw new KeyNotFoundException("Media not found.");

        media.LinkedPostId = null;
        await _mediaRepo.UpdateAsync(media);
        return ToMediaResponse(media);
    }

    public async Task CleanupDeletedAsync()
    {
        var deletedMedia = await _mediaRepo.GetDeletedMediaAsync();

        foreach (var media in deletedMedia)
        {
            // remove from blob storage if still there
            var containerClient = _blobServiceClient
                .GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(media.Filename);
            await blobClient.DeleteIfExistsAsync();
        }
    }

    // Private Helpers for internal use only 

    private async Task<Media> GetOwnedMediaAsync(Guid mediaId, Guid uploaderId)
    {
        var media = await _mediaRepo.GetByMediaIdAsync(mediaId)
            ?? throw new KeyNotFoundException("Media not found.");

        if (media.UploaderId != uploaderId)
            throw new UnauthorizedAccessException("You do not own this media file.");

        return media;
    }

    private static MediaResponse ToMediaResponse(Media m) =>
        new(m.MediaId, m.UploaderId, m.OriginalName, m.Filename,
            m.Url, m.MimeType, m.SizeKb, m.AltText,
            m.LinkedPostId, m.UploadedAt);
}