using InkWellNotification.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellNotification.API.Data;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options)
        : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.NotificationId);
            entity.Property(n => n.Type).HasConversion<string>();
            entity.Property(n => n.Title).HasMaxLength(500);
            entity.Property(n => n.Message).HasMaxLength(1000);

            // index for fast recipient lookups
            entity.HasIndex(n => n.RecipientId);
            entity.HasIndex(n => new { n.RecipientId, n.IsRead });

            entity.HasQueryFilter(n => !n.IsDeleted);
        });
    }
}