using EasyNetQ;

namespace YourService.Messaging;

public class EasyNetQMessageClient : IMessageClient
{
    private readonly IBus _bus;

    public EasyNetQMessageClient(IBus bus)
    {
        _bus = bus;
    }

    public async Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default)
    {
        await _bus.PubSub.PublishAsync(message, cancellationToken);
    }

    public async Task SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default)
    {
        await _bus.PubSub.SubscribeAsync(
            subscriptionId,
            handler,
            cancellationToken: cancellationToken);
    }
}

```csharp
/*
Johan Noter!

private readonly IBus _bus:
EasyNetQ's forbindelse til RabbitMQ. Bruges til at sende og modtage beskeder.

PublishAsync<T>:
Bruger _bus.PubSub.PublishAsync til at sende et C# object som en besked gennem RabbitMQ.

SubscribeAsync<T>:
Bruger _bus.PubSub.SubscribeAsync til at lytte efter beskeder af typen T.
Når en besked kommer, bliver handler-funktionen kørt.

CancellationToken:
Gør det muligt at stoppe publish/subscribe operationen.
*/
```
