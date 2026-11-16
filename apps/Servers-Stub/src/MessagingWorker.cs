using System.Collections.Concurrent;
using Bizcord.Shared.Events;
using EasyNetQ;

namespace ServersStub;

public sealed class MessagingWorker : BackgroundService
{
    private readonly IBus _bus;
    private readonly ILogger<MessagingWorker> _logger;
    private readonly ConcurrentDictionary<Guid, Guid> _systemMessages = new();

    public MessagingWorker(IBus bus, ILogger<MessagingWorker> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bus.PubSub.SubscribeAsync<MembershipSuspendedEvent>(
            "servers-stub-messaging",
            message => HandleMembershipSuspended(message),
            stoppingToken);

        await _bus.PubSub.SubscribeAsync<MembershipReactivatedEvent>(
            "servers-stub-messaging",
            message => HandleMembershipReactivated(message),
            stoppingToken);

        _logger.LogInformation("Messaging is subscribed to MembershipSuspendedEvent and MembershipReactivatedEvent.");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private Task HandleMembershipSuspended(MembershipSuspendedEvent message)
    {
        var systemMessageId = Guid.NewGuid();

        if (!_systemMessages.TryAdd(message.UserId, systemMessageId))
        {
            _logger.LogInformation(
                "System message already posted for user {UserId}; ignoring duplicate.",
                message.UserId);

            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "System message {SystemMessageId} posted: user {UserId} was removed (BanId {BanId}).",
            systemMessageId,
            message.UserId,
            message.BanId);

        return Task.CompletedTask;
    }

    private Task HandleMembershipReactivated(MembershipReactivatedEvent message)
    {
        if (_systemMessages.TryRemove(message.UserId, out var systemMessageId))
        {
            _logger.LogWarning(
                "System message {SystemMessageId} deleted for user {UserId} (UnbanId {UnbanId}). Some participants may already have seen it.",
                systemMessageId,
                message.UserId,
                message.UnbanId);
        }
        else
        {
            _logger.LogInformation(
                "No system message to delete for user {UserId}.",
                message.UserId);
        }

        return Task.CompletedTask;
    }
}