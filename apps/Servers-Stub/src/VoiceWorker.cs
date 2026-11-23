using System.Collections.Concurrent;
using Bizcord.Shared.Events;
using EasyNetQ;

namespace ServersStub;

public sealed class VoiceWorker : BackgroundService
{
    private readonly IBus _bus;
    private readonly ILogger<VoiceWorker> _logger;
    private readonly ConcurrentDictionary<Guid, bool> _voiceBlockedUsers = new();

    public VoiceWorker(IBus bus, ILogger<VoiceWorker> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bus.PubSub.SubscribeAsync<MembershipReactivatedEvent>(
            "servers-stub-voice",
            message => HandleMembershipReactivated(message),
            stoppingToken);

        _logger.LogInformation("Voice is subscribed to MembershipReactivatedEvent.");

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private Task HandleMembershipReactivated(MembershipReactivatedEvent message)
    {
        _voiceBlockedUsers[message.UserId] = false;

        _logger.LogInformation(
            "Voice access restored for user {UserId} (UnbanId {UnbanId}). The earlier call is gone and cannot be restored.",
            message.UserId,
            message.UnbanId);

        _logger.LogInformation(
            "Notify participants: user {UserId} can join voice again. The previous call was ended and will not resume.",
            message.UserId);

        return Task.CompletedTask;
    }
}