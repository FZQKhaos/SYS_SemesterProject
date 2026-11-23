using Bizcord.Shared.Commands;
using Bizcord.Shared.Events;
using Rebus.Bus;
using Rebus.Handlers;

namespace ServersStub.Handlers;

public sealed class SuspendMembershipHandler : IHandleMessages<SuspendMembership>
{
    private readonly IBus _bus;
    private readonly ILogger<SuspendMembershipHandler> _logger;
    private readonly bool _failSuspension;

    public SuspendMembershipHandler(
        IBus bus,
        IConfiguration configuration,
        ILogger<SuspendMembershipHandler> logger)
    {
        _bus = bus;
        _logger = logger;
        _failSuspension = configuration.GetValue<bool>("Stub:FailSuspension");
    }

    public async Task Handle(SuspendMembership message)
    {
        _logger.LogInformation(
            "Received SuspendMembership for user {UserId} (BanId {BanId}). FailSuspension = {FailSuspension}",
            message.UserId,
            message.BanId,
            _failSuspension);

        if (_failSuspension)
        {
            _logger.LogWarning(
                "Simulated business failure for BanId {BanId}. Publishing MembershipSuspensionFailedEvent.",
                message.BanId);

            await _bus.Publish(new MembershipSuspensionFailedEvent
            {
                BanId = message.BanId,
                UserId = message.UserId,
                Reason = "Simulated business failure: membership could not be suspended."
            });

            return;
        }

        await _bus.Publish(new MembershipSuspendedEvent
        {
            BanId = message.BanId,
            UserId = message.UserId,
            SuspendedAt = DateTime.UtcNow
        });
    }
}