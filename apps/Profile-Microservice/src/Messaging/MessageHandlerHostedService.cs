namespace ProfileMicroservice.Messaging;

public sealed class MessageHandlerHostedService<TMessage> : IHostedService, IAsyncDisposable
{
    private readonly IMessageClient _messageClient;
    private readonly IMessageHandler<TMessage> _handler;
    private readonly string _subscriptionId;
    private IAsyncDisposable? _subscription;

    public MessageHandlerHostedService(
        IMessageClient messageClient,
        IMessageHandler<TMessage> handler,
        string subscriptionId)
    {
        _messageClient = messageClient;
        _handler = handler;
        _subscriptionId = subscriptionId;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = await _messageClient.SubscribeAsync<TMessage>(
            _subscriptionId,
            message => _handler.Handle(message, CancellationToken.None),
            cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_subscription is not null)
        {
            await _subscription.DisposeAsync();
            _subscription = null;
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
        services.AddHostedService(sp => new MessageHandlerHostedService<TMessage>(
            sp.GetRequiredService<IMessageClient>(),
            sp.GetRequiredService<IMessageHandler<TMessage>>(),
            subscriptionId));
        return services;
    }
}