using EasyNetQ;

namespace YourService.Messaging;

public class MessageClient : IMessageClient
{
    private readonly IBus _bus;

    public MessageClient(IBus bus)
    {
        _bus = bus;
    }

    public async Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        await _bus.PubSub.PublishAsync(
            message,
            cancellationToken);
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

/*
Johan Noter!

IBus:
EasyNetQ's forbindelse til message brokeren.
Det er kun denne implementation der kender til EasyNetQ.

PublishAsync<T>:
Bruger EasyNetQ PubSub til at sende beskeden videre til RabbitMQ.

SubscribeAsync<T>:
Bruger EasyNetQ PubSub til at oprette en subscription for beskeder af typen T.

subscription:
EasyNetQ returnerer en SubscriptionResult når vi subscriber.
SubscriptionResult implementerer IAsyncDisposable, så vi returnerer kun
den generelle .NET interface og skjuler EasyNetQ detaljerne.

Fordelen:
Resten af microservicen bruger IMessageClient.
Hvis vi senere skifter RabbitMQ/EasyNetQ ud med f.eks. Kafka,
skal resten af applikationen ikke ændres.
*/