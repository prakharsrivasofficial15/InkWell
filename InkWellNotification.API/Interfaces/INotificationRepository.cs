using InkWell.Shared;
using InkWellNotification.API.Models;

namespace InkWellNotification.API.Interfaces;

public interface INotificationRepository : IBaseRepository<Notification>
{
    Task<Notification?> GetByNotificationIdAsync(Guid notificationId);
    Task<IEnumerable<Notification>> GetByRecipientIdAsync(Guid recipientId);
    Task<IEnumerable<Notification>> GetUnreadByRecipientIdAsync(Guid recipientId);
    Task<int> CountUnreadByRecipientIdAsync(Guid recipientId);
    Task<IEnumerable<Notification>> GetByTypeAsync(NotificationType type);
    Task<IEnumerable<Notification>> GetAllNotificationsAsync();
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAllAsReadAsync(Guid recipientId);
    Task DeleteReadAsync(Guid recipientId);
}