using InkWellNotification.API.DTOs;
using InkWellNotification.API.Events;

namespace InkWellNotification.API.Interfaces;

public interface INotificationService
{
    // send operations
    Task<NotificationResponse> SendAsync(SendNotificationRequest request);
    Task SendBulkAsync(SendBulkNotificationRequest request);

    // retrieval
    Task<IEnumerable<NotificationResponse>> GetByRecipientAsync(Guid recipientId);
    Task<IEnumerable<NotificationResponse>> GetAllAsync();
    Task<UnreadCountResponse> GetUnreadCountAsync(Guid recipientId);

    // read state management
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAllReadAsync(Guid recipientId);
    Task DeleteReadAsync(Guid recipientId);
    Task DeleteNotificationAsync(Guid notificationId);

    // event handlers called by Service Bus consumers
    Task HandleCommentAddedAsync(CommentAddedEvent commentEvent);
    Task HandlePostPublishedAsync(PostPublishedEvent postEvent);
}