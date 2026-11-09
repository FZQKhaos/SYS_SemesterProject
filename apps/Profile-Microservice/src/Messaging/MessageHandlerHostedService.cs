namespace ProfileMicroservice.Messaging;

public sealed class MessageHandlerHostedService<TMessage> : BackgroundService
{
    private static readonly TimeSpan SubscriptionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaximumRetryDelay = TimeSpan.FromSeconds(10);

    private readonly IMessageClient _messageClient;
    private readonly IMessageHandler<TMessage> _handler;
    private readonly string _subscriptionId;
    private readonly ILogger<MessageHandlerHostedService<TMessage>> _logger;
    private readonly TimeSpan _initialRetryDelay;
    private readonly TimeSpan _subscriptionTimeout;
    private readonly TaskCompletionSource<bool> _firstSubscription =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WhenSubscribed => _firstSubscription.Task;

    public MessageHandlerHostedService(
        IMessageClient messageClient,
        IMessageHandler<TMessage> handler,
        string subscriptionId,
        ILogger<MessageHandlerHostedService<TMessage>> logger,
        TimeSpan? initialRetryDelay = null,
        TimeSpan? subscriptionTimeout = null)
    {
        _messageClient = messageClient;
        _handler = handler;
        _subscriptionId = subscriptionId;
        _logger = logger;
        _initialRetryDelay = initialRetryDelay ?? TimeSpan.FromMilliseconds(250);
        _subscriptionTimeout = subscriptionTimeout ?? SubscriptionTimeout;

        if (_initialRetryDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(initialRetryDelay));
        }

        if (_subscriptionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(subscriptionTimeout));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Do not make the REST API's startup depend on RabbitMQ availability.
        await Task.Yield();

        var retryDelay = _initialRetryDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            IAsyncDisposable? subscription = null;
            try
            {
                using var attemptTimeout =
                    CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                attemptTimeout.CancelAfter(_subscriptionTimeout);

                subscription = await _messageClient.SubscribeAsync<TMessage>(
                    _subscriptionId,
                    message => _handler.Handle(message, stoppingToken),
                    attemptTimeout.Token).WaitAsync(attemptTimeout.Token);

                _firstSubscription.TrySetResult(true);
                _logger.LogInformation(
                    "Subscribed to {MessageType} with subscription {SubscriptionId}",
                    typeof(TMessage).Name, _subscriptionId);

                // EasyNetQ owns the active subscription. Test its recovery
                // after a later broker disconnect separately. Keep it until shutdown.
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Subscription {SubscriptionId} for {MessageType} failed. " +
                    "Retrying in {RetryDelay}",
                    _subscriptionId, typeof(TMessage).Name, retryDelay);

                try
                {
                    await Task.Delay(retryDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                retryDelay = TimeSpan.FromMilliseconds(
                    Math.Min(retryDelay.TotalMilliseconds * 2,
                        MaximumRetryDelay.TotalMilliseconds));
            }
            finally
            {
                if (subscription is not null)
                {
                    try
                    {
                        await subscription.DisposeAsync();
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception,
                            "Could not cleanly dispose subscription {SubscriptionId}",
                            _subscriptionId);
                    }
                }
            }
        }
    }
}

public static class MessageHandlerServiceCollectionExtensions
{
    public static IServiceCollection AddMessageHandler<TMessage, THandler>(
        this IServiceCollection services,
        string subscriptionId)
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddSingleton<IMessageHandler<TMessage>, THandler>();
        services.AddSingleton(sp => new MessageHandlerHostedService<TMessage>(
            sp.GetRequiredService<IMessageClient>(),
            sp.GetRequiredService<IMessageHandler<TMessage>>(),
            subscriptionId,
            sp.GetRequiredService<ILogger<MessageHandlerHostedService<TMessage>>>()));
        services.AddHostedService<MessageHandlerHostedService<TMessage>>(sp =>
            sp.GetRequiredService<MessageHandlerHostedService<TMessage>>());
        return services;
    }
}
