using Bizcord.Shared.Events;
using EasyNetQ;
using System.Collections.Concurrent;

namespace ServersStub;

public sealed class VoiceWorker : BackgroundService
{
    private const int MaxAttempts = 4;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

    private readonly IBus _bus;
    private readonly ILogger<VoiceWorker> _logger;
    private readonly int _voiceFailures;
    private readonly ConcurrentDictionary<Guid, bool> _voiceBlockedUsers = new();

    public VoiceWorker(
        IBus bus,
        IConfiguration configuration,
        ILogger<VoiceWorker> logger)
    {
        _bus = bus;
        _logger = logger;
        _voiceFailures = configuration.GetValue<int>("Stub:VoiceFailures");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _bus.PubSub.SubscribeAsync<MembershipSuspendedEvent>(
            "servers-stub-voice",
            message => HandleMembershipSuspended(message, stoppingToken),
            stoppingToken);

        await _bus.PubSub.SubscribeAsync<MembershipReactivatedEvent>(
            "servers-stub-voice",
            message => HandleMembershipReactivated(message),
            stoppingToken);

        _logger.LogInformation(
            "Voice is subscribed to MembershipSuspendedEvent. VoiceFailures = {VoiceFailures}",
            _voiceFailures);

        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }

    private async Task HandleMembershipSuspended(
        MembershipSuspendedEvent message,
        CancellationToken cancellationToken)
    {
        _voiceBlockedUsers[message.UserId] = true;

        var terminated = await TerminateCallWithRetryAsync(message, cancellationToken);

        if (!terminated)
        {
            await _bus.PubSub.PublishAsync(
                new CallTerminationFailedEvent
                {
                    BanId = message.BanId,
                    UserId = message.UserId,
                    Attempts = MaxAttempts,
                    Reason = "Voice termination timed out on every attempt."
                },
                cancellationToken);

            return;
        }

        await _bus.PubSub.PublishAsync(
            new CallTerminatedEvent
            {
                BanId = message.BanId,
                UserId = message.UserId,
                TerminatedAt = DateTime.UtcNow
            },
            cancellationToken);
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

    private async Task<bool> TerminateCallWithRetryAsync(
        MembershipSuspendedEvent message,
        CancellationToken cancellationToken)
    {
        var delay = InitialDelay;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                TerminateCall(attempt);

                _logger.LogInformation(
                    "Call terminated for user {UserId} (BanId {BanId}) on attempt {Attempt}.",
                    message.UserId,
                    message.BanId,
                    attempt);

                return true;
            }
            catch (TimeoutException exception) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(
                    exception,
                    "Voice termination attempt {Attempt}/{MaxAttempts} failed for BanId {BanId}. Retrying in {Delay}.",
                    attempt,
                    MaxAttempts,
                    message.BanId,
                    delay);

                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
            catch (TimeoutException exception)
            {
                _logger.LogError(
                    exception,
                    "ALERT: Could not terminate call after {MaxAttempts} attempts for user {UserId} (BanId {BanId}). Manual intervention required. The ban stays in effect.",
                    MaxAttempts,
                    message.UserId,
                    message.BanId);
            }
        }

        return false;
    }

    private void TerminateCall(int attempt)
    {
        if (attempt <= _voiceFailures)
        {
            throw new TimeoutException(
                $"Simulated voice service timeout (attempt {attempt}).");
        }
    }
}