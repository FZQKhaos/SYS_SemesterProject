using Microsoft.Extensions.Logging.Abstractions;
using ProfileMicroservice.Messaging;

namespace tests;

public class MessageHandlerHostedServiceTests
{
    [Fact]
    public async Task FailedFirstSubscription_RetriesAndEventuallySubscribes()
    {
        var client = new SimulatedMessageClient(failFirstAttempt: true);
        using var service = CreateService(client);

        await service.StartAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(1));

        await service.WhenSubscribed.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, client.Attempts);

        await service.StopAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(client.SubscriptionDisposed);
    }

    [Fact]
    public async Task HungSubscription_DoesNotBlockStartup_AndRetriesAfterTimeout()
    {
        var client = new SimulatedMessageClient(hangUntilCancelled: true);
        using var service = CreateService(client);

        // The hosted service must let ASP.NET Core finish starting even if
        // RabbitMQ never completes a subscription attempt.
        await service.StartAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(1));
        await client.AttemptedTwice.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(client.Attempts >= 2);
        Assert.False(service.WhenSubscribed.IsCompleted);

        await service.StopAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));
    }

    private static MessageHandlerHostedService<string> CreateService(
        SimulatedMessageClient client) => new(
        client,
        new NoOpHandler(),
        "test-profile-subscription",
        NullLogger<MessageHandlerHostedService<string>>.Instance,
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromMilliseconds(30));

    private sealed class NoOpHandler : IMessageHandler<string>
    {
        public Task Handle(string message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class SimulatedMessageClient : IMessageClient
    {
        private readonly bool _failFirstAttempt;
        private readonly bool _hangUntilCancelled;
        private readonly TaskCompletionSource<bool> _attempted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _attemptedTwice =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempts;

        public SimulatedMessageClient(
            bool failFirstAttempt = false,
            bool hangUntilCancelled = false)
        {
            _failFirstAttempt = failFirstAttempt;
            _hangUntilCancelled = hangUntilCancelled;
        }

        public int Attempts => Volatile.Read(ref _attempts);
        public bool SubscriptionDisposed { get; private set; }
        public Task Attempted => _attempted.Task;
        public Task AttemptedTwice => _attemptedTwice.Task;

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IAsyncDisposable> SubscribeAsync<T>(
            string subscriptionId,
            Func<T, Task> handler,
            CancellationToken cancellationToken = default)
        {
            var attempt = Interlocked.Increment(ref _attempts);
            _attempted.TrySetResult(true);
            if (attempt >= 2) _attemptedTwice.TrySetResult(true);

            if (_failFirstAttempt && attempt == 1)
            {
                throw new InvalidOperationException("Simulated broker outage");
            }

            if (_hangUntilCancelled)
            {
                return WaitForCancellation(cancellationToken);
            }

            return Task.FromResult<IAsyncDisposable>(new FakeSubscription(this));
        }

        private static async Task<IAsyncDisposable> WaitForCancellation(
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }

        private sealed class FakeSubscription : IAsyncDisposable
        {
            private readonly SimulatedMessageClient _owner;
            public FakeSubscription(SimulatedMessageClient owner) => _owner = owner;
            public ValueTask DisposeAsync()
            {
                _owner.SubscriptionDisposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }
}
