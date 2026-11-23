using Bizcord.Shared.Events;
using EasyNetQ;

namespace ServersStub;

public sealed class MessagingWorker : BackgroundService
{
    private readonly IBus _bus;
    private readonly SystemMessageStore _store;
    private readonly ILogger<MessagingWorker> _logger;

    public MessagingWorker(
        IBus bus,
        SystemMessageStore store,
        ILogger<MessagingWorker> logger)
    {
        _bus = bus;
        _store = store;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bus.PubSub.SubscribeAsync<MembershipReactivatedEvent>(
            "servers-stub-messaging",
            message => HandleMembershipReactivated(message),
            stoppingToken);

        _logger.LogInformation("Messaging is subscribed to MembershipReactivatedEvent.");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private Task HandleMembershipReactivated(MembershipReactivatedEvent message)
    {
        if (_store.TryRemove(message.UserId, out var systemMessageId))
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