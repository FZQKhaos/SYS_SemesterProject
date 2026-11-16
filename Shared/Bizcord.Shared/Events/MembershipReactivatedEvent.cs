namespace Bizcord.Shared.Events;

public class MembershipReactivatedEvent
{
    public Guid UnbanId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ReactivatedAt { get; set; }
}