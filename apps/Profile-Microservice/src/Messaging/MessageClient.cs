using EasyNetQ;

namespace ProfileMicroservice.Messaging;

public class MessageClient : IMessageClient
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(5);
    private readonly IBus _bus;

    public MessageClient(IBus bus)
    {
        _bus = bus;
    }

    public async Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        using var attemptTimeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attemptTimeout.CancelAfter(PublishTimeout);

        try
        {
            await _bus.PubSub.PublishAsync(message, attemptTimeout.Token)
                .WaitAsync(attemptTimeout.Token);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Publishing {typeof(T).Name} exceeded the {PublishTimeout.TotalSeconds}s timeout.",
                exception);
        }
    }

    public async Task<IAsyncDisposable> SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _bus.PubSub.SubscribeAsync(
            subscriptionId,
            handler,
            cancellationToken);

        return subscription;
    }
}
