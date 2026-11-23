using System.Collections.Concurrent;
using Bizcord.Shared.Events;
using EasyNetQ;

namespace ServersStub;

public sealed class ServersWorker : BackgroundService
{
    private readonly IBus _bus;
    private readonly ILogger<ServersWorker> _logger;
    private readonly ConcurrentDictionary<Guid, bool> _suspendedUsers = new();

    public ServersWorker(IBus bus, ILogger<ServersWorker> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bus.PubSub.SubscribeAsync<UserUnbannedEvent>(
            "servers-stub",
            message => HandleUserUnbanned(message, stoppingToken),
            stoppingToken);

        _logger.LogInformation("ServersStub is subscribed to UserUnbannedEvent.");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleUserUnbanned(
        UserUnbannedEvent message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received UserUnbannedEvent for user {UserId} (UnbanId {UnbanId}). Membership is active again.",
            message.UserId,
            message.UnbanId);

        _suspendedUsers[message.UserId] = false;

        await _bus.PubSub.PublishAsync(
            new MembershipReactivatedEvent
            {
                UnbanId = message.UnbanId,
                UserId = message.UserId,
                ReactivatedAt = DateTime.UtcNow
            },
            cancellationToken);
    }
}