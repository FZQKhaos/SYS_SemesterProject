using System.Collections.Concurrent;
using Bizcord.Shared.Events;
using EasyNetQ;

namespace ServersStub;

public sealed class SessionWorker : BackgroundService
{
    private readonly IBus _bus;
    private readonly ILogger<SessionWorker> _logger;
    private readonly ConcurrentDictionary<Guid, bool> _loginBlockedUsers = new();

    public SessionWorker(IBus bus, ILogger<SessionWorker> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bus.PubSub.SubscribeAsync<UserUnbannedEvent>(
            "servers-stub-sessions",
            message => HandleUserUnbanned(message),
            stoppingToken);

        _logger.LogInformation("Sessions is subscribed to UserUnbannedEvent.");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private Task HandleUserUnbanned(UserUnbannedEvent message)
    {
        _loginBlockedUsers[message.UserId] = false;

        _logger.LogInformation(
            "Login allowed again for user {UserId}. Old sessions stay revoked; the user must log in again.",
            message.UserId);

        return Task.CompletedTask;
    }
}