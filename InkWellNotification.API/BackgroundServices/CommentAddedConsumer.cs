using Azure.Messaging.ServiceBus;
using InkWellNotification.API.Events;
using InkWellNotification.API.Interfaces;
using System.Text.Json;

namespace InkWellNotification.API.BackgroundServices;

// listens to Azure Service Bus for comment.added events & creates in-app notifications for post authors and comment repliers
public class CommentAddedConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly ILogger<CommentAddedConsumer> _logger;
    private ServiceBusProcessor? _processor;

    public CommentAddedConsumer(
        IServiceProvider serviceProvider,
        ServiceBusClient serviceBusClient,
        ILogger<CommentAddedConsumer> logger)
    {
        _serviceProvider  = serviceProvider;
        _serviceBusClient = serviceBusClient;
        _logger           = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _serviceBusClient.CreateProcessor(
            topicName: "inkwell-comment-added",
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
            "CommentAddedConsumer started — listening on inkwell-comment-added");

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        try
        {
            var body         = args.Message.Body.ToString();
            var commentEvent = JsonSerializer.Deserialize<CommentAddedEvent>(body);

            if (commentEvent is null)
            {
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            _logger.LogInformation(
                "Received comment.added event for CommentId: {CommentId}",
                commentEvent.CommentId);

            using var scope = _serviceProvider.CreateScope();
            var notifService = scope.ServiceProvider
                .GetRequiredService<INotificationService>();

            await notifService.HandleCommentAddedAsync(commentEvent);
            await args.CompleteMessageAsync(args.Message);

            _logger.LogInformation("Notification created for comment event");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing comment.added message");
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