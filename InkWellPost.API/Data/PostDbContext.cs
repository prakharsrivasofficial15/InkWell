using InkWellPost.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellPost.API.Data;

public class PostDbContext : DbContext
{
    public PostDbContext(DbContextOptions<PostDbContext> options) : base(options) { }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(p => p.PostId);
            entity.HasIndex(p => p.Slug).IsUnique();
            entity.Property(p => p.Status).HasConversion<string>();
            entity.Property(p => p.Title).HasMaxLength(500);
            entity.Property(p => p.Slug).HasMaxLength(600);
            entity.HasQueryFilter(p => !p.IsDeleted);
        });

        modelBuilder.Entity<PostLike>(entity =>
        {
            entity.HasKey(pl => pl.PostLikeId);
            // one like per user per post
            entity.HasIndex(pl => new { pl.PostId, pl.UserId }).IsUnique();
            entity.HasQueryFilter(pl => !pl.IsDeleted);
        });
    }
}