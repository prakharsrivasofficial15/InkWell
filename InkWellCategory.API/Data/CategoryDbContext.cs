using InkWellCategory.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellCategory.API.Data;

public class CategoryDbContext : DbContext
{
    public CategoryDbContext(DbContextOptions<CategoryDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PostCategory> PostCategories => Set<PostCategory>();
    public DbSet<PostTag> PostTags => Set<PostTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(c => c.CategoryId);
            entity.HasIndex(c => c.Slug).IsUnique();
            entity.Property(c => c.Name).HasMaxLength(200);
            entity.Property(c => c.Slug).HasMaxLength(250);
            entity.HasQueryFilter(c => !c.IsDeleted);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(t => t.TagId);
            entity.HasIndex(t => t.Slug).IsUnique();
            entity.Property(t => t.Name).HasMaxLength(100);
            entity.Property(t => t.Slug).HasMaxLength(150);
            entity.HasQueryFilter(t => !t.IsDeleted);
        });

        modelBuilder.Entity<PostCategory>(entity =>
        {
            entity.HasKey(pc => pc.PostCategoryId);
            // one category per post — no duplicates
            entity.HasIndex(pc => new { pc.PostId, pc.CategoryId }).IsUnique();
            entity.HasQueryFilter(pc => !pc.IsDeleted);
        });

        modelBuilder.Entity<PostTag>(entity =>
        {
            entity.HasKey(pt => pt.PostTagId);
            // one tag per post — no duplicates
            entity.HasIndex(pt => new { pt.PostId, pt.TagId }).IsUnique();
            entity.HasQueryFilter(pt => !pt.IsDeleted);
        });
    }
}