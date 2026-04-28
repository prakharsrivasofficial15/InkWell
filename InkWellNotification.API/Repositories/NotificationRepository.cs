using InkWellNotification.API.Data;
using InkWellNotification.API.Interfaces;
using InkWellNotification.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellNotification.API.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;
    public NotificationRepository(NotificationDbContext db) => _db = db;

    public async Task<Notification?> GetByIdAsync(Guid id) =>
        await _db.Notifications.FirstOrDefaultAsync(n => n.NotificationId == id);

    public async Task<Notification?> GetByNotificationIdAsync(Guid notificationId) =>
        await _db.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId);

    public async Task<IEnumerable<Notification>> GetAllAsync() =>
        await _db.Notifications.ToListAsync();

    public async Task<IEnumerable<Notification>> GetAllNotificationsAsync() =>
        await _db.Notifications
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Notification>> GetByRecipientIdAsync(Guid recipientId) =>
        await _db.Notifications
            .Where(n => n.RecipientId == recipientId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Notification>> GetUnreadByRecipientIdAsync(
        Guid recipientId) =>
        await _db.Notifications
            .Where(n => n.RecipientId == recipientId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<int> CountUnreadByRecipientIdAsync(Guid recipientId) =>
        await _db.Notifications
            .CountAsync(n => n.RecipientId == recipientId && !n.IsRead);

    public async Task<IEnumerable<Notification>> GetByTypeAsync(NotificationType type) =>
        await _db.Notifications
            .Where(n => n.Type == type)
            .ToListAsync();

    public async Task<Notification> AddAsync(Notification entity)
    {
        _db.Notifications.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<Notification> UpdateAsync(Notification entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Notifications.Update(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(Guid id)
    {
        var notification = await GetByNotificationIdAsync(id);
        if (notification is null) return;
        notification.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _db.Notifications.AnyAsync(n => n.NotificationId == id);

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await GetByNotificationIdAsync(notificationId);
        if (notification is null) return;
        notification.IsRead    = true;
        notification.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(Guid recipientId)
    {
        var unread = await GetUnreadByRecipientIdAsync(recipientId);
        foreach (var n in unread)
        {
            n.IsRead    = true;
            n.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
    }

    public async Task DeleteReadAsync(Guid recipientId)
    {
        var read = await _db.Notifications
            .Where(n => n.RecipientId == recipientId && n.IsRead)
            .ToListAsync();

        foreach (var n in read)
            n.IsDeleted = true;

        await _db.SaveChangesAsync();
    }
}