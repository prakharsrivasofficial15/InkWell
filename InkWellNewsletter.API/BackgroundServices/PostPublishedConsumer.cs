using Azure.Messaging.ServiceBus;
using InkWellNewsletter.API.Events;
using InkWellNewsletter.API.Interfaces;
using System.Text.Json;

namespace InkWellNewsletter.API.BackgroundServices;

// listens to Azure Service Bus for post.published events
// when post-service publishes a post, this sends emails to all subscribers
public class PostPublishedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<PostPublishedConsumer> _logger;
    private readonly IConfiguration _configuration;
    private ServiceBusProcessor? _processor;

    public PostPublishedConsumer(
        IServiceProvider serviceProvider,
        ServiceBusClient serviceBusClient,
        ILogger<PostPublishedConsumer> logger,
        IConfiguration configuration)
    {
        _serviceProvider  = serviceProvider;
        _serviceBusClient = serviceBusClient;
        _logger           = logger;
        _configuration    = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Debug: Log connection string status
        var connectionString = _configuration["inkwell-servicebus-connection"];
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogError("Service Bus connection string is null or empty!");
            return;
        }
        
        _logger.LogInformation("Service Bus connection string found: {ConnectionString}", 
            connectionString.Substring(0, Math.Min(50, connectionString.Length)) + "...");

        _processor = _serviceBusClient.CreateProcessor(
            "inkwell-post-published",
            "newsletter-sub",
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 1,
                AutoCompleteMessages = false
            });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync   += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation(
            "PostPublishedConsumer started — listening on inkwell-post-published");

        // keep running until app stops
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var body = args.Message.Body.ToString();
            var postEvent = JsonSerializer.Deserialize<PostPublishedEvent>(body);

            if (postEvent is null)
            {
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            _logger.LogInformation(
                "Received post.published event for PostId: {PostId}", postEvent.PostId);

            // use scoped service since INewsletterService is scoped
            using var scope = _serviceProvider.CreateScope();
            var newsletterService = scope.ServiceProvider
                .GetRequiredService<INewsletterService>();

            await newsletterService.SendPostNotificationAsync(postEvent);

            await args.CompleteMessageAsync(args.Message);

            _logger.LogInformation(
                "Post notification emails sent for: {Title}", postEvent.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing post.published message");
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "Service Bus error on {EntityPath}", args.EntityPath);
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _processor?.DisposeAsync().AsTask().Wait();
        base.Dispose();
    }
}