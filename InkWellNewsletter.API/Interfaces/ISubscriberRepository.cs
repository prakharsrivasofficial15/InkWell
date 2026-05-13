using InkWell.Shared;
using InkWellNewsletter.API.Models;

namespace InkWellNewsletter.API.Interfaces;

public interface ISubscriberRepository : IBaseRepository<Subscriber>
{
    Task<Subscriber?> GetBySubscriberIdAsync(Guid subscriberId);
    Task<Subscriber?> GetByEmailAsync(string email);
    Task<Subscriber?> GetByUserIdAsync(Guid userId);
    Task<Subscriber?> GetByTokenAsync(string token);
    Task<IEnumerable<Subscriber>> GetByStatusAsync(SubscriberStatus status);
    Task<bool> EmailExistsAsync(string email);
    Task<int> CountByStatusAsync(SubscriberStatus status);

    // campaign operations
    Task<NewsletterCampaign> AddCampaignAsync(NewsletterCampaign campaign);
    Task<NewsletterCampaign> UpdateCampaignAsync(NewsletterCampaign campaign);
    Task<IEnumerable<NewsletterCampaign>> GetAllCampaignsAsync();
}