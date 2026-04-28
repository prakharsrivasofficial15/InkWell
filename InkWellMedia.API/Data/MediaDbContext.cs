using InkWellMedia.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellMedia.API.Data;

public class MediaDbContext : DbContext
{
    public MediaDbContext(DbContextOptions<MediaDbContext> options) : base(options) { }

    public DbSet<Media> MediaFiles => Set<Media>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Media>(entity =>
        {
            entity.HasKey(m => m.MediaId);
            entity.Property(m => m.OriginalName).HasMaxLength(500);
            entity.Property(m => m.Filename).HasMaxLength(500);
            entity.Property(m => m.MimeType).HasMaxLength(100);

            // soft delete global filter
            entity.HasQueryFilter(m => !m.IsDeleted);
        });
    }
}