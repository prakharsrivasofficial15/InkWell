using InkWellNotification.API.DTOs;
using InkWellNotification.API.Events;
using InkWellNotification.API.Interfaces;
using InkWellNotification.API.Models;

namespace InkWellNotification.API.Services;

public class NotificationServiceImpl : INotificationService
{
    private readonly INotificationRepository _notifRepo;
    private readonly IConfiguration _config;

    public NotificationServiceImpl(
        INotificationRepository notifRepo,
        IConfiguration config)
    {
        _notifRepo = notifRepo;
        _config = config;
    }

    // Send Operations
    public async Task<NotificationResponse> SendAsync(SendNotificationRequest request)
    {
        if (!Enum.TryParse<NotificationType>(request.Type, out var notifType))
            throw new ArgumentException($"Invalid notification type: {request.Type}");

        var notification = new Notification
        {
            RecipientId = request.RecipientId,
            ActorId = request.ActorId,
            Type = notifType,
            Title = request.Title,
            Message = request.Message,
            RelatedId = request.RelatedId,
            RelatedType = request.RelatedType,
            IsRead = false
        };

        await _notifRepo.AddAsync(notification);
        return ToNotificationResponse(notification);
    }

    public async Task SendBulkAsync(SendBulkNotificationRequest request)
    {
        if (!Enum.TryParse<NotificationType>(request.Type, out var notifType))
            throw new ArgumentException($"Invalid notification type: {request.Type}");

        foreach (var recipientId in request.RecipientIds)
        {
            var notification = new Notification
            {
                RecipientId = recipientId,
                Type = notifType,
                Title = request.Title,
                Message = request.Message,
                RelatedId = request.RelatedId,
                RelatedType = request.RelatedType,
                IsRead = false
            };

            await _notifRepo.AddAsync(notification);
        }
    }

    // Retrieval
    public async Task<IEnumerable<NotificationResponse>> GetByRecipientAsync(
        Guid recipientId)
    {
        var notifications = await _notifRepo.GetByRecipientIdAsync(recipientId);
        return notifications.Select(ToNotificationResponse);
    }

    public async Task<IEnumerable<NotificationResponse>> GetAllAsync()
    {
        var notifications = await _notifRepo.GetAllNotificationsAsync();
        return notifications.Select(ToNotificationResponse);
    }

    public async Task<UnreadCountResponse> GetUnreadCountAsync(Guid recipientId)
    {
        var count = await _notifRepo.CountUnreadByRecipientIdAsync(recipientId);
        return new UnreadCountResponse(recipientId, count);
    }

    // Read State Management
    public async Task MarkAsReadAsync(Guid notificationId) =>
        await _notifRepo.MarkAsReadAsync(notificationId);

    public async Task MarkAllReadAsync(Guid recipientId) =>
        await _notifRepo.MarkAllAsReadAsync(recipientId);

    public async Task DeleteReadAsync(Guid recipientId) =>
        await _notifRepo.DeleteReadAsync(recipientId);

    public async Task DeleteNotificationAsync(Guid notificationId) =>
        await _notifRepo.DeleteAsync(notificationId);

    // Event Handlers (called by Service Bus consumers)

    public async Task HandleCommentAddedAsync(CommentAddedEvent commentEvent)
    {
        // Case 1: New top-level comment → notify post author
        if (commentEvent.ParentCommentId == null)
        {
            await _notifRepo.AddAsync(new Notification
            {
                RecipientId = commentEvent.PostAuthorId,
                ActorId = commentEvent.AuthorId,
                Type = NotificationType.NEW_COMMENT,
                Title = "New comment on your post",
                Message = $"Someone commented: \"{TruncateMessage(commentEvent.Content)}\"",
                RelatedId = commentEvent.PostId,
                RelatedType = "Post"
            });
        }
        else
        {
            // Case 2: Reply to comment → notify the parent commenter
            await _notifRepo.AddAsync(new Notification
            {
                RecipientId = commentEvent.ParentCommentAuthorId ?? commentEvent.PostAuthorId,
                ActorId = commentEvent.AuthorId,
                Type = NotificationType.COMMENT_REPLY,
                Title = "Someone replied to your comment",
                Message = $"Reply: \"{TruncateMessage(commentEvent.Content)}\"",
                RelatedId = commentEvent.CommentId,
                RelatedType = "Comment"
            });
        }
    }

    public async Task HandlePostPublishedAsync(PostPublishedEvent postEvent)
    {
        // In-app notification for new post published
        // In a full system you'd look up followers here
        // For now we create a broadcast notification
        await _notifRepo.AddAsync(new Notification
        {
            RecipientId = postEvent.AuthorId,
            ActorId = postEvent.AuthorId,
            Type = NotificationType.NEW_POST,
            Title = "Your post is now live!",
            Message = $"\"{postEvent.Title}\" has been published.",
            RelatedId = postEvent.PostId,
            RelatedType = "Post"
        });
    }

    // Private Helpers for message truncation and response mapping
    private static string TruncateMessage(string content, int maxLength = 80) =>
        content.Length <= maxLength
            ? content
            : content[..maxLength] + "...";

    private static NotificationResponse ToNotificationResponse(Notification n) =>
        new(n.NotificationId, n.RecipientId, n.ActorId,
            n.Type.ToString(), n.Title, n.Message,
            n.RelatedId, n.RelatedType, n.IsRead, n.CreatedAt);
}