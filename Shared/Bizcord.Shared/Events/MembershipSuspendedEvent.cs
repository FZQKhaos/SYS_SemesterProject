namespace Bizcord.Shared.Events;

public class MembershipSuspendedEvent
{
    public Guid BanId { get; set; }
    public Guid UserId { get; set; }
    public DateTime SuspendedAt { get; set; }
}