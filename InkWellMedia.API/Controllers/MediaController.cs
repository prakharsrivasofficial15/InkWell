using System.Security.Claims;
using InkWell.Shared.DTOs;
using InkWellMedia.API.DTOs;
using InkWellMedia.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InkWellMedia.API.Controllers;

[ApiController]
[Route("api/media")]
[Produces("application/json")]
public class MediaController : ControllerBase
{
    private readonly IMediaService _mediaService;

    public MediaController(IMediaService mediaService) =>
        _mediaService = mediaService;

    // Author + Admin Endpoints

    // Upload a media file to Azure Blob Storage: Max 10MB
    [HttpPost("upload")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<MediaResponse>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        try
        {
            var uploaderId = GetCurrentUserId();
            var media = await _mediaService.UploadMediaAsync(uploaderId, file);
            return CreatedAtAction(nameof(GetById),
                new { mediaId = media.MediaId },
                new ApiResponse<MediaResponse>(true, "File uploaded successfully.", media));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Get a media file by ID
    [HttpGet("{mediaId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<MediaResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid mediaId)
    {
        try
        {
            var media = await _mediaService.GetMediaByIdAsync(mediaId);
            return Ok(new ApiResponse<MediaResponse>(true, "Media fetched.", media));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Get all media uploaded by the current user
    [HttpGet("my")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<MediaResponse>>), 200)]
    public async Task<IActionResult> GetMyMedia()
    {
        var uploaderId = GetCurrentUserId();
        var media = await _mediaService.GetMediaByUploaderAsync(uploaderId);
        return Ok(new ApiResponse<IEnumerable<MediaResponse>>(
            true, "Media fetched.", media));
    }

    // Get media linked to a specific post
    [HttpGet("post/{postId}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<MediaResponse>>), 200)]
    public async Task<IActionResult> GetByPost(Guid postId)
    {
        var media = await _mediaService.GetMediaByPostAsync(postId);
        return Ok(new ApiResponse<IEnumerable<MediaResponse>>(
            true, "Post media fetched.", media));
    }

    // Update alt text for accessibility
    [HttpPut("{mediaId}/alttext")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<MediaResponse>), 200)]
    public async Task<IActionResult> UpdateAltText(
        Guid mediaId, [FromBody] UpdateAltTextRequest request)
    {
        try
        {
            var uploaderId = GetCurrentUserId();
            var media = await _mediaService.UpdateAltTextAsync(
                mediaId, uploaderId, request.AltText);
            return Ok(new ApiResponse<MediaResponse>(true, "Alt text updated.", media));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Link media to a post
    [HttpPut("{mediaId}/link")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<MediaResponse>), 200)]
    public async Task<IActionResult> LinkToPost(
        Guid mediaId, [FromBody] LinkPostRequest request)
    {
        try
        {
            var media = await _mediaService.LinkToPostAsync(mediaId, request.PostId);
            return Ok(new ApiResponse<MediaResponse>(true, "Media linked to post.", media));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Unlink media from a post
    [HttpPut("{mediaId}/unlink")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<MediaResponse>), 200)]
    public async Task<IActionResult> UnlinkFromPost(Guid mediaId)
    {
        try
        {
            var media = await _mediaService.UnlinkFromPostAsync(mediaId);
            return Ok(new ApiResponse<MediaResponse>(true, "Media unlinked.", media));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Delete media file & also removes from the database and Blob Storage
    [HttpDelete("{mediaId}")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Delete(Guid mediaId)
    {
        try
        {
            var uploaderId = GetCurrentUserId();
            await _mediaService.DeleteMediaAsync(mediaId, uploaderId);
            return Ok(new ApiResponse<object>(true, "Media deleted.", null));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Admin Only Endpoints

    // Get all media platform-wide (Admin only)
    [HttpGet("all")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<MediaResponse>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var media = await _mediaService.GetAllMediaAsync();
        return Ok(new ApiResponse<IEnumerable<MediaResponse>>(
            true, "All media fetched.", media));
    }

    // Cleanup soft-deleted files from Blob Storage (Access: Admin only)
    [HttpPost("cleanup")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Cleanup()
    {
        await _mediaService.CleanupDeletedAsync();
        return Ok(new ApiResponse<object>(true, "Cleanup completed.", null));
    }

    // Helper method to get current user ID from JWT token
    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? throw new UnauthorizedAccessException("User ID not in token.");
        return Guid.Parse(sub);
    }
}