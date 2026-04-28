using Azure.Messaging.ServiceBus;
using InkWellNotification.API.Events;
using InkWellNotification.API.Interfaces;
using System.Text.Json;

namespace InkWellNotification.API.BackgroundServices;

// listens for post.published events to create in-app notifications
public class PostPublishedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<PostPublishedConsumer> _logger;
    private ServiceBusProcessor? _processor;

    public PostPublishedConsumer(
        IServiceProvider serviceProvider,
        ServiceBusClient serviceBusClient,
        ILogger<PostPublishedConsumer> logger)
    {
        _serviceProvider  = serviceProvider;
        _serviceBusClient = serviceBusClient;
        _logger           = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _serviceBusClient.CreateProcessor(
            topicName: "inkwell-post-published",
            subscriptionName: "notification-sub",
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls   = 1,
                AutoCompleteMessages = false
            });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync   += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);

        _logger.LogInformation(
            "PostPublishedConsumer started — listening on inkwell-post-published");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var body      = args.Message.Body.ToString();
            var postEvent = JsonSerializer.Deserialize<PostPublishedEvent>(body);

            if (postEvent is null)
            {
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            _logger.LogInformation(
                "Received post.published event for PostId: {PostId}", postEvent.PostId);

            using var scope = _serviceProvider.CreateScope();
            var notifService = scope.ServiceProvider
                .GetRequiredService<INotificationService>();

            await notifService.HandlePostPublishedAsync(postEvent);
            await args.CompleteMessageAsync(args.Message);
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