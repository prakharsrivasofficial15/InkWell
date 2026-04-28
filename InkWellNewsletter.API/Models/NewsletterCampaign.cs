using InkWell.Shared;

namespace InkWellNewsletter.API.Models;

public enum CampaignStatus { DRAFT, SENT, FAILED }

public class NewsletterCampaign : BaseEntity
{
    public Guid CampaignId { get; set; } = Guid.NewGuid();
    public string Subject { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public CampaignStatus Status { get; set; } = CampaignStatus.DRAFT;
    public DateTime? SentAt { get; set; }
    public int RecipientCount { get; set; }
    public Guid SentByAdminId { get; set; }
}