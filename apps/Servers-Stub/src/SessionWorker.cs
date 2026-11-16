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
        await _bus.PubSub.SubscribeAsync<UserBannedEvent>(
            "servers-stub-sessions",
            message => HandleUserBanned(message),
            stoppingToken);

        await _bus.PubSub.SubscribeAsync<UserUnbannedEvent>(
            "servers-stub-sessions",
            message => HandleUserUnbanned(message),
            stoppingToken);

        await _bus.PubSub.SubscribeAsync<MembershipSuspensionFailedEvent>(
            "servers-stub-sessions",
            message => HandleSuspensionFailed(message),
            stoppingToken);

        _logger.LogInformation("Sessions is subscribed to UserBannedEvent, UserUnbannedEvent and MembershipSuspensionFailedEvent.");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private Task HandleUserBanned(UserBannedEvent message)
    {
        _loginBlockedUsers[message.UserId] = true;

        _logger.LogInformation(
            "All sessions revoked for user {UserId} (BanId {BanId}). New logins are refused.",
            message.UserId,
            message.BanId);

        return Task.CompletedTask;
    }

    private Task HandleUserUnbanned(UserUnbannedEvent message)
    {
        AllowLogin(message.UserId);

        return Task.CompletedTask;
    }

    private Task HandleSuspensionFailed(MembershipSuspensionFailedEvent message)
    {
        AllowLogin(message.UserId);

        return Task.CompletedTask;
    }

    private void AllowLogin(Guid userId)
    {
        _loginBlockedUsers[userId] = false;

        _logger.LogInformation(
            "Login allowed again for user {UserId}. Old sessions stay revoked; the user must log in again.",
            userId);
    }
}