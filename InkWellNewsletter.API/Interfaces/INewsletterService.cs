using InkWellNewsletter.API.DTOs;
using InkWellNewsletter.API.Events;

namespace InkWellNewsletter.API.Interfaces;

public interface INewsletterService
{
    // subscription lifecycle
    Task<SubscriberResponse> SubscribeAsync(SubscribeRequest request);
    Task ConfirmSubscriptionAsync(string token);
    Task UnsubscribeAsync(string token);
    Task<SubscriberResponse> GetSubscriberByEmailAsync(string email);
    Task<IEnumerable<SubscriberResponse>> GetAllSubscribersAsync();
    Task<SubscriberCountResponse> GetSubscriberCountAsync();
    Task UpdatePreferencesAsync(Guid subscriberId, UpdatePreferencesRequest request);

    // email dispatch
    Task<CampaignResponse> SendNewsletterAsync(
        Guid adminId, SendNewsletterRequest request);
    Task SendPostNotificationAsync(PostPublishedEvent postEvent);
    Task SendWelcomeEmailAsync(string email, string fullName);
    Task SendConfirmationEmailAsync(string email, string fullName, string token);
}