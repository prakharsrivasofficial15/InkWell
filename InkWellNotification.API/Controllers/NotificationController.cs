using System.Security.Claims;
using InkWell.Shared.DTOs;
using InkWellNotification.API.DTOs;
using InkWellNotification.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InkWellNotification.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notifService;

    public NotificationController(INotificationService notifService) =>
        _notifService = notifService;

    // Authenticated User Endpoints

    // Get all notifications for current user
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<NotificationResponse>>), 200)]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userId        = GetCurrentUserId();
        var notifications = await _notifService.GetByRecipientAsync(userId);
        return Ok(new ApiResponse<IEnumerable<NotificationResponse>>(
            true, "Notifications fetched.", notifications));
    }

    // Get unread notification count for current user
    [HttpGet("unread/count")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UnreadCountResponse>), 200)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetCurrentUserId();
        var count  = await _notifService.GetUnreadCountAsync(userId);
        return Ok(new ApiResponse<UnreadCountResponse>(
            true, "Unread count fetched.", count));
    }

    // Mark a single notification as read
    [HttpPut("{notificationId}/read")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> MarkAsRead(Guid notificationId)
    {
        await _notifService.MarkAsReadAsync(notificationId);
        return Ok(new ApiResponse<object>(true, "Notification marked as read.", null));
    }

    // Mark all notifications as read for current user
    [HttpPut("read/all")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetCurrentUserId();
        await _notifService.MarkAllReadAsync(userId);
        return Ok(new ApiResponse<object>(true, "All notifications marked as read.", null));
    }

    // Delete all read notifications for current user
    [HttpDelete("read")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> DeleteRead()
    {
        var userId = GetCurrentUserId();
        await _notifService.DeleteReadAsync(userId);
        return Ok(new ApiResponse<object>(true, "Read notifications deleted.", null));
    }

    // Delete a specific notification
    [HttpDelete("{notificationId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> Delete(Guid notificationId)
    {
        await _notifService.DeleteNotificationAsync(notificationId);
        return Ok(new ApiResponse<object>(true, "Notification deleted.", null));
    }

    // Admin Endpoints

    // Send notification to specific users (Admin only)
    [HttpPost("send")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<NotificationResponse>), 201)]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest request)
    {
        try
        {
            var notification = await _notifService.SendAsync(request);
            return CreatedAtAction(nameof(GetMyNotifications), null,
                new ApiResponse<NotificationResponse>(
                    true, "Notification sent.", notification));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Send notification to multiple users (Admin only)
    [HttpPost("send/bulk")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public async Task<IActionResult> SendBulk([FromBody] SendBulkNotificationRequest request)
    {
        try
        {
            await _notifService.SendBulkAsync(request);
            return Ok(new ApiResponse<object>(
                true,
                $"Notifications sent to {request.RecipientIds.Count} users.",
                null));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiResponse<object>(false, ex.Message, null));
        }
    }

    // Get all notifications platform-wide (Admin only)
    [HttpGet("all")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<NotificationResponse>>), 200)]
    public async Task<IActionResult> GetAll()
    {
        var notifications = await _notifService.GetAllAsync();
        return Ok(new ApiResponse<IEnumerable<NotificationResponse>>(
            true, "All notifications fetched.", notifications));
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