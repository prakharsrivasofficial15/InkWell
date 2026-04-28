using Azure.Communication.Email;
using Azure.Messaging.ServiceBus;
using InkWellNewsletter.API.DTOs;
using InkWellNewsletter.API.Events;
using InkWellNewsletter.API.Interfaces;
using InkWellNewsletter.API.Models;

namespace InkWellNewsletter.API.Services;

public class NewsletterServiceImpl : INewsletterService
{
    private readonly ISubscriberRepository _subscriberRepo;
    private readonly EmailClient _emailClient;
    private readonly IConfiguration _config;

    public NewsletterServiceImpl(
        ISubscriberRepository subscriberRepo,
        EmailClient emailClient,
        IConfiguration config)
    {
        _subscriberRepo = subscriberRepo;
        _emailClient    = emailClient;
        _config         = config;
    }

    // ── Subscription Lifecycle ────────────────────────────────────────────────

    public async Task<SubscriberResponse> SubscribeAsync(SubscribeRequest request)
    {
        // check if already subscribed
        if (await _subscriberRepo.EmailExistsAsync(request.Email))
        {
            var existing = await _subscriberRepo.GetByEmailAsync(request.Email);

            // if unsubscribed before, reactivate with new token
            if (existing!.Status == SubscriberStatus.UNSUBSCRIBED)
            {
                existing.Status      = SubscriberStatus.PENDING;
                existing.Token       = Guid.NewGuid().ToString();
                existing.TokenExpiry = DateTime.UtcNow.AddHours(24);
                await _subscriberRepo.UpdateAsync(existing);
                await SendConfirmationEmailAsync(
                    existing.Email, existing.FullName, existing.Token);
                return ToSubscriberResponse(existing);
            }

            throw new InvalidOperationException("Email already subscribed.");
        }

        var subscriber = new Subscriber
        {
            Email       = request.Email.ToLowerInvariant(),
            FullName    = request.FullName,
            UserId      = request.UserId,
            Status      = SubscriberStatus.PENDING,
            Preferences = request.Preferences,
            Token       = Guid.NewGuid().ToString(),
            TokenExpiry = DateTime.UtcNow.AddHours(24)
        };

        await _subscriberRepo.AddAsync(subscriber);

        // send double opt-in confirmation email
        await SendConfirmationEmailAsync(
            subscriber.Email, subscriber.FullName, subscriber.Token);

        return ToSubscriberResponse(subscriber);
    }

    public async Task ConfirmSubscriptionAsync(string token)
    {
        var subscriber = await _subscriberRepo.GetByTokenAsync(token)
            ?? throw new KeyNotFoundException("Invalid confirmation token.");

        if (subscriber.TokenExpiry < DateTime.UtcNow)
            throw new InvalidOperationException(
                "Confirmation token has expired. Please subscribe again.");

        subscriber.Status = SubscriberStatus.ACTIVE;
        subscriber.Token  = Guid.NewGuid().ToString(); // rotate token after use
        await _subscriberRepo.UpdateAsync(subscriber);

        // send welcome email after confirmation
        await SendWelcomeEmailAsync(subscriber.Email, subscriber.FullName);
    }

    public async Task UnsubscribeAsync(string token)
    {
        var subscriber = await _subscriberRepo.GetByTokenAsync(token)
            ?? throw new KeyNotFoundException("Invalid unsubscribe token.");

        subscriber.Status          = SubscriberStatus.UNSUBSCRIBED;
        subscriber.UnsubscribedAt  = DateTime.UtcNow;
        await _subscriberRepo.UpdateAsync(subscriber);
    }

    public async Task<SubscriberResponse> GetSubscriberByEmailAsync(string email)
    {
        var subscriber = await _subscriberRepo.GetByEmailAsync(email)
            ?? throw new KeyNotFoundException("Subscriber not found.");
        return ToSubscriberResponse(subscriber);
    }

    public async Task<IEnumerable<SubscriberResponse>> GetAllSubscribersAsync()
    {
        var subscribers = await _subscriberRepo.GetAllAsync();
        return subscribers.Select(ToSubscriberResponse);
    }

    public async Task<SubscriberCountResponse> GetSubscriberCountAsync()
    {
        var active      = await _subscriberRepo.CountByStatusAsync(SubscriberStatus.ACTIVE);
        var pending     = await _subscriberRepo.CountByStatusAsync(SubscriberStatus.PENDING);
        var unsubscribed = await _subscriberRepo.CountByStatusAsync(SubscriberStatus.UNSUBSCRIBED);

        return new SubscriberCountResponse(
            active + pending + unsubscribed,
            active, pending, unsubscribed);
    }

    public async Task UpdatePreferencesAsync(
        Guid subscriberId, UpdatePreferencesRequest request)
    {
        var subscriber = await _subscriberRepo.GetBySubscriberIdAsync(subscriberId)
            ?? throw new KeyNotFoundException("Subscriber not found.");

        subscriber.Preferences = request.Preferences;
        await _subscriberRepo.UpdateAsync(subscriber);
    }

    // ── Email Dispatch ────────────────────────────────────────────────────────

    public async Task<CampaignResponse> SendNewsletterAsync(
        Guid adminId, SendNewsletterRequest request)
    {
        // get target subscribers
        var subscribers = await _subscriberRepo.GetByStatusAsync(SubscriberStatus.ACTIVE);

        // filter by preference tag if specified
        if (!string.IsNullOrEmpty(request.FilterByPreference))
        {
            subscribers = subscribers.Where(s =>
                s.Preferences != null &&
                s.Preferences.Contains(request.FilterByPreference));
        }

        var recipientList = subscribers.ToList();

        // create campaign record
        var campaign = new NewsletterCampaign
        {
            Subject        = request.Subject,
            HtmlContent    = request.HtmlContent,
            Status         = CampaignStatus.DRAFT,
            SentByAdminId  = adminId,
            RecipientCount = recipientList.Count
        };

        await _subscriberRepo.AddCampaignAsync(campaign);

        try
        {
            // send to each subscriber with unsubscribe link
            foreach (var subscriber in recipientList)
            {
                var unsubscribeLink = BuildUnsubscribeLink(subscriber.Token);
                var htmlWithFooter  = AddUnsubscribeFooter(
                    request.HtmlContent, unsubscribeLink);

                await SendEmailAsync(
                    subscriber.Email,
                    subscriber.FullName,
                    request.Subject,
                    htmlWithFooter);
            }

            campaign.Status = CampaignStatus.SENT;
            campaign.SentAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            campaign.Status = CampaignStatus.FAILED;
            Console.WriteLine($"Campaign failed: {ex.Message}");
        }

        await _subscriberRepo.UpdateCampaignAsync(campaign);
        return ToCampaignResponse(campaign);
    }

    public async Task SendPostNotificationAsync(PostPublishedEvent postEvent)
    {
        // send to all ACTIVE subscribers when new post published
        var subscribers = await _subscriberRepo.GetByStatusAsync(SubscriberStatus.ACTIVE);

        var subject  = $"New Post: {postEvent.Title}";
        var postUrl  = $"http://localhost:4200/blog/{postEvent.Slug}";
        var htmlBody = $"""
            <h2>New post published!</h2>
            <h3>{postEvent.Title}</h3>
            <p>{postEvent.Excerpt ?? "Check out the latest post on InkWell."}</p>
            <a href="{postUrl}" style="background:#3b82f6;color:white;padding:10px 20px;
               text-decoration:none;border-radius:5px;">Read Now</a>
            """;

        foreach (var subscriber in subscribers)
        {
            var unsubscribeLink  = BuildUnsubscribeLink(subscriber.Token);
            var htmlWithFooter   = AddUnsubscribeFooter(htmlBody, unsubscribeLink);
            await SendEmailAsync(
                subscriber.Email, subscriber.FullName, subject, htmlWithFooter);
        }
    }

    public async Task SendWelcomeEmailAsync(string email, string fullName)
    {
        var subject  = "Welcome to InkWell! 🎉";
        var htmlBody = $"""
            <h2>Welcome to InkWell, {fullName}!</h2>
            <p>Your subscription is now active. You'll receive updates whenever
               new posts are published.</p>
            <p>Happy reading!</p>
            <p>— The InkWell Team</p>
            """;

        await SendEmailAsync(email, fullName, subject, htmlBody);
    }

    public async Task SendConfirmationEmailAsync(
        string email, string fullName, string token)
    {
        var confirmLink = BuildConfirmationLink(token);
        var subject     = "Confirm your InkWell subscription";
        var htmlBody    = $"""
            <h2>Hi {fullName},</h2>
            <p>Please confirm your subscription to InkWell by clicking the button below.
               This link expires in 24 hours.</p>
            <a href="{confirmLink}" style="background:#3b82f6;color:white;padding:10px 20px;
               text-decoration:none;border-radius:5px;">Confirm Subscription</a>
            <p>If you did not subscribe, you can ignore this email.</p>
            """;

        await SendEmailAsync(email, fullName, subject, htmlBody);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private async Task SendEmailAsync(
        string toEmail, string toName, string subject, string htmlBody)
    {
        var fromAddress = _config["inkwell-acs-mail-from"]
            ?? throw new InvalidOperationException("Mail from address not configured.");

        var emailMessage = new EmailMessage(
            senderAddress: fromAddress,
            recipients: new EmailRecipients(new[]
            {
                new EmailAddress(toEmail, toName)
            }),
            content: new EmailContent(subject)
            {
                Html = htmlBody
            });

        try
        {
            await _emailClient.SendAsync(
                Azure.WaitUntil.Started, emailMessage);
        }
        catch (Exception ex)
        {
            // log but don't throw - email failure shouldn't break the flow
            Console.WriteLine($"Email send failed to {toEmail}: {ex.Message}");
        }
    }

    private string BuildConfirmationLink(string token) =>
        $"http://localhost:5006/api/newsletter/confirm?token={token}";

    private string BuildUnsubscribeLink(string token) =>
        $"http://localhost:5006/api/newsletter/unsubscribe?token={token}";

    private static string AddUnsubscribeFooter(string html, string unsubscribeLink) =>
        html + $"""
            <hr/>
            <p style="font-size:12px;color:#888;">
                Don't want to receive these emails?
                <a href="{unsubscribeLink}">Unsubscribe</a>
            </p>
            """;

    private static SubscriberResponse ToSubscriberResponse(Subscriber s) =>
        new(s.SubscriberId, s.Email, s.FullName, s.Status.ToString(),
            s.Preferences, s.SubscribedAt, s.UnsubscribedAt);

    private static CampaignResponse ToCampaignResponse(NewsletterCampaign c) =>
        new(c.CampaignId, c.Subject, c.Status.ToString(),
            c.RecipientCount, c.SentAt, c.CreatedAt);
}