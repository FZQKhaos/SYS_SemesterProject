using Bizcord.Shared.Commands;
using Bizcord.Shared.Events;
using Rebus.Bus;
using Rebus.Handlers;

namespace ServersStub.Handlers;

public sealed class TerminateCallHandler : IHandleMessages<TerminateCall>
{
    private const int MaxAttempts = 4;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

    private readonly IBus _bus;
    private readonly ILogger<TerminateCallHandler> _logger;
    private readonly int _voiceFailures;

    public TerminateCallHandler(
        IBus bus,
        IConfiguration configuration,
        ILogger<TerminateCallHandler> logger)
    {
        _bus = bus;
        _logger = logger;
        _voiceFailures = configuration.GetValue<int>("Stub:VoiceFailures");
    }

    public async Task Handle(TerminateCall message)
    {
        _logger.LogInformation(
            "Received TerminateCall for user {UserId} (BanId {BanId}). VoiceFailures = {VoiceFailures}",
            message.UserId,
            message.BanId,
            _voiceFailures);

        var terminated = await TerminateWithRetryAsync(message);

        if (!terminated)
        {
            await _bus.Publish(new CallTerminationFailedEvent
            {
                BanId = message.BanId,
                UserId = message.UserId,
                Attempts = MaxAttempts,
                Reason = "Voice termination timed out on every attempt."
            });

            return;
        }

        await _bus.Publish(new CallTerminatedEvent
        {
            BanId = message.BanId,
            UserId = message.UserId,
            TerminatedAt = DateTime.UtcNow
        });
    }

    private async Task<bool> TerminateWithRetryAsync(TerminateCall message)
    {
        var delay = InitialDelay;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                SimulateVoiceCall(attempt);

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

                await Task.Delay(delay);
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

    private void SimulateVoiceCall(int attempt)
    {
        if (attempt <= _voiceFailures)
        {
            throw new TimeoutException(
                $"Simulated voice service timeout (attempt {attempt}).");
        }
    }
}