using System.Collections.Concurrent;
using Bizcord.Shared.Events;
using EasyNetQ;

namespace ServersStub;

public sealed class ServersWorker : BackgroundService
{
    private readonly IBus _bus;
    private readonly ILogger<ServersWorker> _logger;
    private readonly bool _failSuspension;
    private readonly ConcurrentDictionary<Guid, bool> _suspendedUsers = new();

    public ServersWorker(
        IBus bus,
        IConfiguration configuration,
        ILogger<ServersWorker> logger)
    {
        _bus = bus;
        _logger = logger;
        _failSuspension = configuration.GetValue<bool>("Stub:FailSuspension");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bus.PubSub.SubscribeAsync<UserBannedEvent>(
            "servers-stub",
            message => HandleUserBanned(message, stoppingToken),
            stoppingToken);
        
        await _bus.PubSub.SubscribeAsync<UserUnbannedEvent>(
            "servers-stub",
            message => HandleUserUnbanned(message, stoppingToken),
            stoppingToken);

        _logger.LogInformation(
            "ServersStub is subscribed to UserBannedEvent. FailSuspension = {FailSuspension}",
            _failSuspension);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleUserBanned(
        UserBannedEvent message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received UserBannedEvent for user {UserId} (BanId {BanId}).",
            message.UserId,
            message.BanId);

        if (_failSuspension)
        {
            _logger.LogWarning(
                "Simulated business failure for BanId {BanId}. Publishing MembershipSuspensionFailedEvent.",
                message.BanId);
            
            await _bus.PubSub.PublishAsync(
                new MembershipSuspensionFailedEvent
                {
                    BanId = message.BanId,
                    UserId = message.UserId,
                    Reason = "Simulated business failure: membership could not be suspended."
                },
                cancellationToken);

            return;
        }

        _suspendedUsers[message.UserId] = true;

        await _bus.PubSub.PublishAsync(
            new MembershipSuspendedEvent
            {
                BanId = message.BanId,
                UserId = message.UserId,
                SuspendedAt = DateTime.UtcNow
            },
            cancellationToken);
    }
    
    private async Task HandleUserUnbanned(
        UserUnbannedEvent message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Received UserUnbannedEvent for user {UserId} (UnbanId {UnbanId}). Membership is active again.",
            message.UserId,
            message.UnbanId);

        _suspendedUsers[message.UserId] = false;

        await _bus.PubSub.PublishAsync(
            new MembershipReactivatedEvent
            {
                UnbanId = message.UnbanId,
                UserId = message.UserId,
                ReactivatedAt = DateTime.UtcNow
            },
            cancellationToken);
    }
}