using Bizcord.Shared.Commands;
using Bizcord.Shared.Events;
using ProfileMicroservice.Services;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace ProfileMicroservice.Sagas;

public class BanSaga :
    Saga<BanSagaData>,
    IAmInitiatedBy<UserBannedEvent>,
    IHandleMessages<MembershipSuspendedEvent>,
    IHandleMessages<MembershipSuspensionFailedEvent>,
    IHandleMessages<CallTerminatedEvent>,
    IHandleMessages<CallTerminationFailedEvent>
{
    private readonly IBus _bus;
    private readonly IProfileService _profiles;
    private readonly IBanSagaTracker _tracker;
    private readonly ILogger<BanSaga> _logger;

    public BanSaga(
        IBus bus,
        IProfileService profiles,
        IBanSagaTracker tracker,
        ILogger<BanSaga> logger)
    {
        _bus = bus;
        _profiles = profiles;
        _tracker = tracker;
        _logger = logger;
    }

    // Fortæller Rebus: match beskedens BanId med saga-datas BanId.
    protected override void CorrelateMessages(ICorrelationConfig<BanSagaData> config)
    {
        config.Correlate<UserBannedEvent>(m => m.BanId, d => d.BanId);
        config.Correlate<MembershipSuspendedEvent>(m => m.BanId, d => d.BanId);
        config.Correlate<MembershipSuspensionFailedEvent>(m => m.BanId, d => d.BanId);
        config.Correlate<CallTerminatedEvent>(m => m.BanId, d => d.BanId);
        config.Correlate<CallTerminationFailedEvent>(m => m.BanId, d => d.BanId);
    }

    // Trin 1 er allerede sket i Profile (IsBanned = true). Sagaen starter her.
    public async Task Handle(UserBannedEvent message)
    {
        Data.BanId = message.BanId;
        Data.UserId = message.UserId;

        _tracker.Record(message.BanId, BanSagaState.Started);

        _logger.LogInformation(
            "Ban saga started for user {UserId} (BanId {BanId}). Sending SuspendMembership.",
            message.UserId,
            message.BanId);

        await _bus.Send(new SuspendMembership
        {
            BanId = message.BanId,
            UserId = message.UserId
        });
    }

    // Trin 2 lykkedes. Nu er vi forbi pivot-punktet, så vi går kun fremad.
    public async Task Handle(MembershipSuspendedEvent message)
    {
        if (Data.MembershipSuspended)
        {
            _logger.LogInformation(
                "Duplicate MembershipSuspendedEvent ignored (BanId {BanId}).",
                message.BanId);

            return; // dublet: beskeden er allerede behandlet
        }

        Data.MembershipSuspended = true;
        _tracker.Record(message.BanId, BanSagaState.Suspended);

        _logger.LogInformation(
            "Membership suspended for user {UserId} (BanId {BanId}). Sending TerminateCall.",
            message.UserId,
            message.BanId);

        await _bus.Send(new TerminateCall
        {
            BanId = message.BanId,
            UserId = message.UserId
        });
    }

    // Trin 2 fejlede (forretningsfejl): kompenser trin 1 ved at unban.
    public Task Handle(MembershipSuspensionFailedEvent message)
    {
        _profiles.Unban(message.UserId.ToString());

        Data.IsFailed = true;
        Data.FailureReason = message.Reason;
        Data.CompletedAt = DateTime.UtcNow;
        _tracker.Record(message.BanId, BanSagaState.Compensated);

        _logger.LogWarning(
            "Membership suspension failed for user {UserId} (BanId {BanId}). Reason: {Reason} Profile unbanned (compensated). Saga complete.",
            message.UserId,
            message.BanId,
            message.Reason);

        MarkAsComplete();
        return Task.CompletedTask;
    }

    // Trin 3 lykkedes: sagaen er færdig.
    public Task Handle(CallTerminatedEvent message)
    {
        Data.CallTerminated = true;
        Data.IsCompleted = true;
        Data.CompletedAt = DateTime.UtcNow;
        _tracker.Record(message.BanId, BanSagaState.Completed);

        _logger.LogInformation(
            "Call terminated for user {UserId} (BanId {BanId}). Saga complete.",
            message.UserId,
            message.BanId);

        MarkAsComplete();
        return Task.CompletedTask;
    }

    // Trin 3 gav op efter alle forsøg. Banen bliver stående, og et menneske må kigge på det.
    public Task Handle(CallTerminationFailedEvent message)
    {
        Data.IsFailed = true;
        Data.FailureReason = message.Reason;
        Data.CompletedAt = DateTime.UtcNow;
        _tracker.Record(message.BanId, BanSagaState.NeedsIntervention);

        _logger.LogError(
            "Call termination failed for user {UserId} (BanId {BanId}) after {Attempts} attempts. Reason: {Reason} The ban stays. Manual intervention required.",
            message.UserId,
            message.BanId,
            message.Attempts,
            message.Reason);

        MarkAsComplete();
        return Task.CompletedTask;
    }
}