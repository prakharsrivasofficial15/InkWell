using InkWellComment.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellComment.API.Data;

public class CommentDbContext : DbContext
{
    public CommentDbContext(DbContextOptions<CommentDbContext> options) : base(options) { }

    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentLike> CommentLikes => Set<CommentLike>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(c => c.CommentId);
            entity.Property(c => c.Status).HasConversion<string>();
            entity.Property(c => c.Content).HasMaxLength(2000);

            // global filter for soft delete
            entity.HasQueryFilter(c => !c.IsDeleted);
        });

        modelBuilder.Entity<CommentLike>(entity =>
        {
            entity.HasKey(cl => cl.CommentLikeId);

            // one like per user per comment
            entity.HasIndex(cl => new { cl.CommentId, cl.UserId }).IsUnique();
            entity.HasQueryFilter(cl => !cl.IsDeleted);
        });
    }
}