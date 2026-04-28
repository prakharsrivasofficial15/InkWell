using System.Security.Claims;
using InkWell.Shared.DTOs;
using InkWellNewsletter.API.DTOs;
using InkWellNewsletter.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InkWellNewsletter.API.Controllers;

[ApiController]
[Route("api/newsletter")]
[Produces("application/json")]
public class NewsletterController : ControllerBase
{
    private readonly INewsletterService _newsletterService;

    public NewsletterController(INewsletterService newsletterService) =>
        _newsletterService = newsletterService;

    // ── Public Endpoints ──────────────────────────────────────────────────────

    /// <summary>Subscribe to newsletter. Triggers double opt-in email.</summary>
    [HttpPost("subscribe")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
    {
        try
        {
            var result = await _newsletterService.SubscribeAsync(request);
            return Ok(new ApiResponse<SubscriberResponse>(
                true,
                "Subscription initiated. Please check your email to confirm.",
                result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>Confirm subscription via token link from email.</summary>
    [HttpGet("confirm")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Confirm([FromQuery] string token)
    {
        try
        {
            await _newsletterService.ConfirmSubscriptionAsync(token);
            return Ok(new ApiResponse<object>(
                true, "Subscription confirmed! Welcome to InkWell.", null));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>One-click unsubscribe via token link from email.</summary>
    [HttpGet("unsubscribe")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Unsubscribe([FromQuery] string token)
    {
        try
        {
            await _newsletterService.UnsubscribeAsync(token);
            return Ok(new ApiResponse<object>(
                true, "You have been unsubscribed successfully.", null));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    /// <summary>Update subscription preferences.</summary>
    [HttpPut("{subscriberId}/preferences")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> UpdatePreferences(
        Guid subscriberId, [FromBody] UpdatePreferencesRequest request)
    {
        try
        {
            await _newsletterService.UpdatePreferencesAsync(subscriberId, request);
            return Ok(new ApiResponse<object>(true, "Preferences updated.", null));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // ── Admin Endpoints ───────────────────────────────────────────────────────

    /// <summary>Get all subscribers. Admin only.</summary>
    [HttpGet("subscribers")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<SubscriberResponse>>), 200)]
    public async Task<IActionResult> GetAllSubscribers()
    {
        var subscribers = await _newsletterService.GetAllSubscribersAsync();
        return Ok(new ApiResponse<IEnumerable<SubscriberResponse>>(
            true, "Subscribers fetched.", subscribers));
    }

    /// <summary>Get subscriber count breakdown. Admin only.</summary>
    [HttpGet("subscribers/count")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<SubscriberCountResponse>), 200)]
    public async Task<IActionResult> GetCount()
    {
        var count = await _newsletterService.GetSubscriberCountAsync();
        return Ok(new ApiResponse<SubscriberCountResponse>(
            true, "Count fetched.", count));
    }

    /// <summary>Send a newsletter campaign. Admin only.</summary>
    [HttpPost("send")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<CampaignResponse>), 200)]
    public async Task<IActionResult> SendNewsletter([FromBody] SendNewsletterRequest request)
    {
        try
        {
            var adminId = GetCurrentUserId();
            var campaign = await _newsletterService.SendNewsletterAsync(adminId, request);
            return Ok(new ApiResponse<CampaignResponse>(
                true, $"Newsletter sent to {campaign.RecipientCount} subscribers.", campaign));
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub")
               ?? throw new UnauthorizedAccessException("User ID not in token.");
        return Guid.Parse(sub);
    }
}