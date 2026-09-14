namespace YourService.Messaging;

public interface IMessageClient
{
    Task PublishAsync<T>(
        T message,
        CancellationToken cancellationToken = default);

    Task<IAsyncDisposable> SubscribeAsync<T>(
        string subscriptionId,
        Func<T, Task> handler,
        CancellationToken cancellationToken = default);
}

/*
Johan Noter!

Task PublishAsync<T>: “Publish a message of any type T asynchronously.”
Basically en måde at konventere et C# object til en besked og sende den til f.eks. RabbitMQ uden at blokere tråden.

Task SubscribeAsync<T>: “Subscribe to messages of type T, and when one arrives, run this handler.”
Lytter efter beskeder af typen T på en bestemt subscription, og kører handler-funktionen når en besked kommer.

subscriptionId: Identificerer den subscription der skal lyttes på.

handler: Funktionen der skal køres, når en besked modtages.

CancellationToken: Gør det muligt at stoppe publish/subscribe operationen igen.

*/