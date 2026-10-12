using ProfileMicroservice.Messaging;
namespace tests;

internal sealed class FakeMessageClient : IMessageClient
{
    public List<object> Published { get; } = new();
    public T? SinglePublished<T>() where T : class
    {
        return Published.OfType<T>().SingleOrDefault();
    }

    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        Published.Add(message);
        return Task.CompletedTask;
    }
    
    public Task<IAsyncDisposable> SubscribeAsync<T>(string subscriptionId, 
        Func<T, 
        Task> handler, 
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}