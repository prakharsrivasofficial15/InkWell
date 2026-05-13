using System.Security.Claims;
using InkWell.Shared.DTOs;
using InkWellComment.API.DTOs;
using InkWellComment.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InkWellComment.API.Controllers;

[ApiController]
[Route("api/comments")]
[Produces("application/json")]
public class CommentController : ControllerBase
{
    private readonly ICommentService _commentService;

    public CommentController(ICommentService commentService) =>
        _commentService = commentService;

    // Public Endpoints

    // Get all comments for a post with threaded replies
    [HttpGet("post/{postId}")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CommentResponse>>), 200)]
    public async Task<IActionResult> GetByPost(Guid postId)
    {
        var comments = await _commentService.GetCommentsByPostAsync(postId);
        return Ok(new ApiResponse<IEnumerable<CommentResponse>>(
            true, "Comments fetched.", comments));
    }

    // Get a single comment with its replies
    [HttpGet("{commentId}")]
    [ProducesResponseType(typeof(ApiResponse<CommentResponse>), 200)]
    public async Task<IActionResult> GetById(Guid commentId)
    {
        try
        {
            var comment = await _commentService.GetCommentByIdAsync(commentId);
            return Ok(new ApiResponse<CommentResponse>(true, "Comment fetched.", comment));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Get replies for a specific comment
    [HttpGet("{commentId}/replies")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CommentResponse>>), 200)]
    public async Task<IActionResult> GetReplies(Guid commentId)
    {
        var replies = await _commentService.GetRepliesAsync(commentId);
        return Ok(new ApiResponse<IEnumerable<CommentResponse>>(
            true, "Replies fetched.", replies));
    }

    // Get comment count for a post
    [HttpGet("post/{postId}/count")]
    [ProducesResponseType(typeof(ApiResponse<int>), 200)]
    public async Task<IActionResult> GetCount(Guid postId)
    {
        var count = await _commentService.GetCommentCountAsync(postId);
        return Ok(new ApiResponse<int>(true, "Count fetched.", count));
    }

    // Authenticated Endpoints

    // Add a top-level comment or reply: READER, AUTHOR, ADMIN
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CommentResponse>), 201)]
    public async Task<IActionResult> Add([FromBody] AddCommentRequest request)
    {
        try
        {
            var authorId = GetCurrentUserId();
            var comment = await _commentService.AddCommentAsync(authorId, request);
            return CreatedAtAction(nameof(GetById),
                new { commentId = comment.CommentId },
                new ApiResponse<CommentResponse>(true, "Comment added.", comment));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Update own comment
    [HttpPut("{commentId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CommentResponse>), 200)]
    public async Task<IActionResult> Update(Guid commentId, [FromBody] UpdateCommentRequest request)
    {
        try
        {
            var authorId = GetCurrentUserId();
            var comment = await _commentService.UpdateCommentAsync(commentId, authorId, request);
            return Ok(new ApiResponse<CommentResponse>(true, "Comment updated.", comment));
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

    // Delete own comment & also deletes all replies
    [HttpDelete("{commentId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Delete(Guid commentId)
    {
        try
        {
            var authorId = GetCurrentUserId();
            await _commentService.DeleteCommentAsync(commentId, authorId);
            return Ok(new ApiResponse<object>(true, "Comment deleted.", null));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    // Like a comment
    [HttpPost("{commentId}/like")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Like(Guid commentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _commentService.LikeCommentAsync(commentId, userId);
            return Ok(new ApiResponse<object>(true, "Comment liked.", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Unlike a comment
    [HttpDelete("{commentId}/like")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Unlike(Guid commentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _commentService.UnlikeCommentAsync(commentId, userId);
            return Ok(new ApiResponse<object>(true, "Comment unliked.", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Author + Admin Endpoints

    // Approve a pending comment: AUTHOR (own posts), ADMIN (all)
    [HttpPut("{commentId}/approve")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<CommentResponse>), 200)]
    public async Task<IActionResult> Approve(Guid commentId)
    {
        try
        {
            var comment = await _commentService.ApproveCommentAsync(commentId);
            return Ok(new ApiResponse<CommentResponse>(true, "Comment approved.", comment));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Reject a comment: AUTHOR (own posts), ADMIN (all)
    [HttpPut("{commentId}/reject")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<CommentResponse>), 200)]
    public async Task<IActionResult> Reject(Guid commentId)
    {
        try
        {
            var comment = await _commentService.RejectCommentAsync(commentId);
            return Ok(new ApiResponse<CommentResponse>(true, "Comment rejected.", comment));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
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