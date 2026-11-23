using Bizcord.Shared.Events;
using Rebus.Handlers;

namespace ServersStub.Handlers;

public sealed class PostSystemMessageHandler : IHandleMessages<MembershipSuspendedEvent>
{
    private readonly SystemMessageStore _store;
    private readonly ILogger<PostSystemMessageHandler> _logger;

    public PostSystemMessageHandler(
        SystemMessageStore store,
        ILogger<PostSystemMessageHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task Handle(MembershipSuspendedEvent message)
    {
        var systemMessageId = Guid.NewGuid();

        if (!_store.TryAdd(message.UserId, systemMessageId))
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
}