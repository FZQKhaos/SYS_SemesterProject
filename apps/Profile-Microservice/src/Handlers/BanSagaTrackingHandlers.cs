using Bizcord.Shared.Events;
using ProfileMicroservice.Messaging;
using ProfileMicroservice.Services;

namespace ProfileMicroservice.Handlers;

public sealed class BanStartedTrackingHandler : IMessageHandler<UserBannedEvent>
{
    private readonly IBanSagaTracker _tracker;

    public BanStartedTrackingHandler(IBanSagaTracker tracker)
    {
        _tracker = tracker;
    }

    public Task Handle(UserBannedEvent message, CancellationToken cancellationToken)
    {
        _tracker.Record(message.BanId, BanSagaState.Started);

        return Task.CompletedTask;
    }
}

public sealed class BanSuspendedTrackingHandler : IMessageHandler<MembershipSuspendedEvent>
{
    private readonly IBanSagaTracker _tracker;

    public BanSuspendedTrackingHandler(IBanSagaTracker tracker)
    {
        _tracker = tracker;
    }

    public Task Handle(MembershipSuspendedEvent message, CancellationToken cancellationToken)
    {
        _tracker.Record(message.BanId, BanSagaState.Suspended);

        return Task.CompletedTask;
    }
}

public sealed class BanCompletedTrackingHandler : IMessageHandler<CallTerminatedEvent>
{
    private readonly IBanSagaTracker _tracker;

    public BanCompletedTrackingHandler(IBanSagaTracker tracker)
    {
        _tracker = tracker;
    }

    public Task Handle(CallTerminatedEvent message, CancellationToken cancellationToken)
    {
        _tracker.Record(message.BanId, BanSagaState.Completed);

        return Task.CompletedTask;
    }
}

public sealed class BanNeedsInterventionTrackingHandler : IMessageHandler<CallTerminationFailedEvent>
{
    private readonly IBanSagaTracker _tracker;

    public BanNeedsInterventionTrackingHandler(IBanSagaTracker tracker)
    {
        _tracker = tracker;
    }

    public Task Handle(CallTerminationFailedEvent message, CancellationToken cancellationToken)
    {
        _tracker.Record(message.BanId, BanSagaState.NeedsIntervention);

        return Task.CompletedTask;
    }
}