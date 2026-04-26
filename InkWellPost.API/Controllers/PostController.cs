using System.Security.Claims;
using InkWell.Shared.DTOs;
using InkWellPost.API.DTOs;
using InkWellPost.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InkWellPost.API.Controllers;

[ApiController]
[Route("api/posts")]
[Produces("application/json")]
public class PostController : ControllerBase
{
    private readonly IPostService _postService;

    public PostController(IPostService postService) =>
        _postService = postService;

    // Public Endpoints

    // Get published post feed
    // Cached in Redis
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PostSummaryResponse>>), 200)]
    public async Task<IActionResult> GetPublished()
    {
        var posts = await _postService.GetPublishedPostsAsync();
        return Ok(new ApiResponse<IEnumerable<PostSummaryResponse>>(
            true, "Posts fetched.", posts));
    }

    // Get a single post by slug URL
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<PostResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        try
        {
            // increment view count using session cookie
            var sessionId = HttpContext.Session.Id;
            await _postService.IncrementViewsAsync(
                (await _postService.GetPostBySlugAsync(slug)).PostId, sessionId);

            var post = await _postService.GetPostBySlugAsync(slug);
            return Ok(new ApiResponse<PostResponse>(true, "Post fetched.", post));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Search posts by keyword
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PostSummaryResponse>>), 200)]
    public async Task<IActionResult> Search([FromQuery] string keyword)
    {
        var posts = await _postService.SearchPostsAsync(keyword);
        return Ok(new ApiResponse<IEnumerable<PostSummaryResponse>>(
            true, "Search results.", posts));
    }

    // Get all posts by a specific author
    [HttpGet("author/{authorId}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PostSummaryResponse>>), 200)]
    public async Task<IActionResult> GetByAuthor(Guid authorId)
    {
        var posts = await _postService.GetPostsByAuthorAsync(authorId);
        return Ok(new ApiResponse<IEnumerable<PostSummaryResponse>>(
            true, "Author posts fetched.", posts));
    }

    // Author/Admin Endpoints

    // Create a new post as DRAFT
    [HttpPost]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PostResponse>), 201)]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest request)
    {
        try
        {
            var authorId = GetCurrentUserId();
            var post = await _postService.CreatePostAsync(authorId, request);
            return CreatedAtAction(nameof(GetBySlug),
                new { slug = post.Slug },
                new ApiResponse<PostResponse>(true, "Post created.", post));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Update a post: Author can only update own posts
    [HttpPut("{postId}")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PostResponse>), 200)]
    public async Task<IActionResult> Update(Guid postId, [FromBody] UpdatePostRequest request)
    {
        try
        {
            var authorId = GetCurrentUserId();
            var post = await _postService.UpdatePostAsync(postId, authorId, request);
            return Ok(new ApiResponse<PostResponse>(true, "Post updated.", post));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Publish a draft post
    [HttpPut("{postId}/publish")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PostResponse>), 200)]
    public async Task<IActionResult> Publish(Guid postId)
    {
        try
        {
            var authorId = GetCurrentUserId();
            var post = await _postService.PublishPostAsync(postId, authorId);
            return Ok(new ApiResponse<PostResponse>(true, "Post published.", post));
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

    // Unpublish a post
    [HttpPut("{postId}/unpublish")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PostResponse>), 200)]
    public async Task<IActionResult> Unpublish(Guid postId)
    {
        try
        {
            var authorId = GetCurrentUserId();
            var post = await _postService.UnpublishPostAsync(postId, authorId);
            return Ok(new ApiResponse<PostResponse>(true, "Post unpublished.", post));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // Delete a post, Soft delete
    [HttpDelete("{postId}")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Delete(Guid postId)
    {
        try
        {
            var authorId = GetCurrentUserId();
            await _postService.DeletePostAsync(postId, authorId);
            return Ok(new ApiResponse<object>(true, "Post deleted.", null));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // Like a post
    [HttpPost("{postId}/like")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Like(Guid postId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _postService.LikePostAsync(postId, userId);
            return Ok(new ApiResponse<object>(true, "Post liked.", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Unlike a post
    [HttpDelete("{postId}/like")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Unlike(Guid postId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _postService.UnlikePostAsync(postId, userId);
            return Ok(new ApiResponse<object>(true, "Post unliked.", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Get post stats Author only for own posts
    [HttpGet("{postId}/stats")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PostStatsResponse>), 200)]
    public async Task<IActionResult> GetStats(Guid postId)
    {
        try
        {
            var stats = await _postService.GetPostStatsAsync(postId);
            return Ok(new ApiResponse<PostStatsResponse>(true, "Stats fetched.", stats));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Get post count for an author
    [HttpGet("count/{authorId}")]
    [ProducesResponseType(typeof(ApiResponse<int>), 200)]
    public async Task<IActionResult> GetCount(Guid authorId)
    {
        var count = await _postService.GetPostCountAsync(authorId);
        return Ok(new ApiResponse<int>(true, "Count fetched.", count));
    }

    // Helper

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? throw new UnauthorizedAccessException("User ID not in token.");
        return Guid.Parse(sub);
    }
}