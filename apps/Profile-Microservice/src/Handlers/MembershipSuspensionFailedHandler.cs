using Bizcord.Shared.Events;
using ProfileMicroservice.Messaging;
using ProfileMicroservice.Services;

namespace ProfileMicroservice.Handlers;

public sealed class MembershipSuspensionFailedHandler
    : IMessageHandler<MembershipSuspensionFailedEvent>
{
    private readonly IProfileService _profiles;
    private readonly IBanSagaTracker _tracker;

    public MembershipSuspensionFailedHandler(
        IProfileService profiles,
        IBanSagaTracker tracker)
    {
        _profiles = profiles;
        _tracker = tracker;
    }

    public Task Handle(
        MembershipSuspensionFailedEvent message,
        CancellationToken cancellationToken)
    {
        _profiles.Unban(message.UserId.ToString());
        _tracker.Record(message.BanId, BanSagaState.Compensated);

        return Task.CompletedTask;
    }
}