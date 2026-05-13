using InkWell.Shared.DTOs;
using InkWellCategory.API.DTOs;
using InkWellCategory.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InkWellCategory.API.Controllers;

[ApiController]
[Route("api/tags")]
[Produces("application/json")]
public class TagController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public TagController(ICategoryService categoryService) =>
        _categoryService = categoryService;

    // Public Endpoints

    // Get all tags
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TagResponse>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var tags = await _categoryService.GetAllTagsAsync();
        return Ok(new ApiResponse<IEnumerable<TagResponse>>(true, "Tags fetched.", tags));
    }

    // Get trending tags by post count
    [HttpGet("trending")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TagResponse>>), 200)]
    public async Task<IActionResult> GetTrending([FromQuery] int count = 10)
    {
        var tags = await _categoryService.GetTrendingTagsAsync(count);
        return Ok(new ApiResponse<IEnumerable<TagResponse>>(true, "Trending tags fetched.", tags));
    }

    // Get a tag by slug
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<TagResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        try
        {
            var tag = await _categoryService.GetTagBySlugAsync(slug);
            return Ok(new ApiResponse<TagResponse>(true, "Tag fetched.", tag));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Get tags for a specific post
    [HttpGet("post/{postId}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TagResponse>>), 200)]
    public async Task<IActionResult> GetByPost(Guid postId)
    {
        var tags = await _categoryService.GetTagsByPostAsync(postId);
        return Ok(new ApiResponse<IEnumerable<TagResponse>>(true, "Post tags fetched.", tags));
    }

    // Admin Endpoints

    // Create a new tag (Admin only)
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<TagResponse>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request)
    {
        try
        {
            var tag = await _categoryService.CreateTagAsync(request);
            return CreatedAtAction(nameof(GetBySlug),
                new { slug = tag.Slug },
                new ApiResponse<TagResponse>(true, "Tag created.", tag));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Delete a tag (Admin only)
    [HttpDelete("{tagId}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Delete(Guid tagId)
    {
        await _categoryService.DeleteTagAsync(tagId);
        return Ok(new ApiResponse<object>(true, "Tag deleted.", null));
    }

    // Add a tag to a post (AUTHOR + ADMIN)
    [HttpPost("post/{postId}/add/{tagId}")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> AddToPost(Guid postId, Guid tagId)
    {
        try
        {
            await _categoryService.AddTagToPostAsync(postId, tagId);
            return Ok(new ApiResponse<object>(true, "Tag added to post.", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Remove a tag from a post (AUTHOR + ADMIN)
    [HttpDelete("post/{postId}/remove/{tagId}")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> RemoveFromPost(Guid postId, Guid tagId)
    {
        await _categoryService.RemoveTagFromPostAsync(postId, tagId);
        return Ok(new ApiResponse<object>(true, "Tag removed from post.", null));
    }
}