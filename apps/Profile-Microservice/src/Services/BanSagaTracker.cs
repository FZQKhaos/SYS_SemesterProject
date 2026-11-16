using System.Collections.Concurrent;

namespace ProfileMicroservice.Services;

public enum BanSagaState
{
    Started = 1,
    Suspended = 2,
    Completed = 3,
    Compensated = 4,
    NeedsIntervention = 5
}

public interface IBanSagaTracker
{
    void Record(Guid banId, BanSagaState state);

    BanSagaState? Get(Guid banId);
}

public sealed class BanSagaTracker : IBanSagaTracker
{
    private readonly ConcurrentDictionary<Guid, BanSagaState> _states = new();

    public void Record(Guid banId, BanSagaState state)
    {
        _states.AddOrUpdate(
            banId,
            state,
            (_, current) => ShouldReplace(current, state) ? state : current);
    }

    public BanSagaState? Get(Guid banId)
    {
        return _states.TryGetValue(banId, out var state) ? state : null;
    }

    private static bool ShouldReplace(BanSagaState current, BanSagaState next)
    {
        return (current is BanSagaState.Started or BanSagaState.Suspended)
               && next > current;
    }
}