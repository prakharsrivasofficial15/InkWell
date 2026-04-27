using InkWellAuth.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellAuth.API.Data;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            // use UserId as the primary key (not BaseEntity.Id)
            entity.HasKey(u => u.UserId);

            // enforce unique email + username at DB level
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();

            // store role enum as string so it's readable in DB
            entity.Property(u => u.Role)
                  .HasConversion<string>();

            entity.Property(u => u.Email).HasMaxLength(256);
            entity.Property(u => u.Username).HasMaxLength(100);
            entity.Property(u => u.FullName).HasMaxLength(200);

            // global query filter - soft delete, never show deleted records
            entity.HasQueryFilter(u => !u.IsDeleted);
        });
    }
}