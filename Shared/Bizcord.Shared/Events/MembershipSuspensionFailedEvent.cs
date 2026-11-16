namespace Bizcord.Shared.Events;

public class MembershipSuspensionFailedEvent
{
    public Guid BanId { get; set; }
    public Guid UserId { get; set; }
    public required string Reason { get; set; }
}