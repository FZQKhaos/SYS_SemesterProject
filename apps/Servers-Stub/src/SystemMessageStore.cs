using System.Collections.Concurrent;

namespace ServersStub;

public sealed class SystemMessageStore
{
    private readonly ConcurrentDictionary<Guid, Guid> _messages = new();

    public bool TryAdd(Guid userId, Guid systemMessageId)
    {
        return _messages.TryAdd(userId, systemMessageId);
    }

    public bool TryRemove(Guid userId, out Guid systemMessageId)
    {
        return _messages.TryRemove(userId, out systemMessageId);
    }
}