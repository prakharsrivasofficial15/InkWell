using InkWellNewsletter.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellNewsletter.API.Data;

public class NewsletterDbContext : DbContext
{
    public NewsletterDbContext(DbContextOptions<NewsletterDbContext> options)
        : base(options) { }

    public DbSet<Subscriber> Subscribers => Set<Subscriber>();
    public DbSet<NewsletterCampaign> Campaigns => Set<NewsletterCampaign>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Subscriber>(entity =>
        {
            entity.HasKey(s => s.SubscriberId);
            entity.HasIndex(s => s.Email).IsUnique();
            entity.HasIndex(s => s.Token).IsUnique();
            entity.Property(s => s.Status).HasConversion<string>();
            entity.Property(s => s.Email).HasMaxLength(256);
            entity.HasQueryFilter(s => !s.IsDeleted);
        });

        modelBuilder.Entity<NewsletterCampaign>(entity =>
        {
            entity.HasKey(c => c.CampaignId);
            entity.Property(c => c.Status).HasConversion<string>();
            entity.HasQueryFilter(c => !c.IsDeleted);
        });
    }
}