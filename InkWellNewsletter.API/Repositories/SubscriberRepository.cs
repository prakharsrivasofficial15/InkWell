using InkWellNewsletter.API.Data;
using InkWellNewsletter.API.Interfaces;
using InkWellNewsletter.API.Models;
using Microsoft.EntityFrameworkCore;

namespace InkWellNewsletter.API.Repositories;

public class SubscriberRepository : ISubscriberRepository
{
    private readonly NewsletterDbContext _db;
    public SubscriberRepository(NewsletterDbContext db) => _db = db;

    public async Task<Subscriber?> GetByIdAsync(Guid id) =>
        await _db.Subscribers.FirstOrDefaultAsync(s => s.SubscriberId == id);

    public async Task<Subscriber?> GetBySubscriberIdAsync(Guid subscriberId) =>
        await _db.Subscribers.FirstOrDefaultAsync(s => s.SubscriberId == subscriberId);

    public async Task<Subscriber?> GetByEmailAsync(string email) =>
        await _db.Subscribers.FirstOrDefaultAsync(s => s.Email == email);

    public async Task<Subscriber?> GetByUserIdAsync(Guid userId) =>
        await _db.Subscribers.FirstOrDefaultAsync(s => s.UserId == userId);

    public async Task<Subscriber?> GetByTokenAsync(string token) =>
        await _db.Subscribers.FirstOrDefaultAsync(s => s.Token == token);

    public async Task<IEnumerable<Subscriber>> GetAllAsync() =>
        await _db.Subscribers.ToListAsync();

    public async Task<IEnumerable<Subscriber>> GetByStatusAsync(SubscriberStatus status) =>
        await _db.Subscribers
            .Where(s => s.Status == status)
            .ToListAsync();

    public async Task<bool> EmailExistsAsync(string email) =>
        await _db.Subscribers.AnyAsync(s => s.Email == email);

    public async Task<int> CountByStatusAsync(SubscriberStatus status) =>
        await _db.Subscribers.CountAsync(s => s.Status == status);

    public async Task<Subscriber> AddAsync(Subscriber entity)
    {
        _db.Subscribers.Add(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task<Subscriber> UpdateAsync(Subscriber entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _db.Subscribers.Update(entity);
        await _db.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(Guid id)
    {
        var subscriber = await GetBySubscriberIdAsync(id);
        if (subscriber is null) return;
        subscriber.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(Guid id) =>
        await _db.Subscribers.AnyAsync(s => s.SubscriberId == id);

    public async Task<NewsletterCampaign> AddCampaignAsync(NewsletterCampaign campaign)
    {
        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync();
        return campaign;
    }

    public async Task<NewsletterCampaign> UpdateCampaignAsync(NewsletterCampaign campaign)
    {
        campaign.UpdatedAt = DateTime.UtcNow;
        _db.Campaigns.Update(campaign);
        await _db.SaveChangesAsync();
        return campaign;
    }

    public async Task<IEnumerable<NewsletterCampaign>> GetAllCampaignsAsync() =>
        await _db.Campaigns
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
}