using Bizcord.Shared.Events;
using Rebus.Handlers;

namespace ServersStub.Handlers;

public sealed class SessionsBanHandler :
    IHandleMessages<UserBannedEvent>,
    IHandleMessages<MembershipSuspensionFailedEvent>
{
    private readonly ILogger<SessionsBanHandler> _logger;

    public SessionsBanHandler(ILogger<SessionsBanHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserBannedEvent message)
    {
        _logger.LogInformation(
            "All sessions revoked for user {UserId} (BanId {BanId}). New logins are refused.",
            message.UserId,
            message.BanId);

        return Task.CompletedTask;
    }

    public Task Handle(MembershipSuspensionFailedEvent message)
    {
        _logger.LogInformation(
            "Login allowed again for user {UserId} (BanId {BanId}). Old sessions stay revoked; the user must log in again.",
            message.UserId,
            message.BanId);

        return Task.CompletedTask;
    }
}