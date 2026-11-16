namespace Bizcord.Shared.Events;

public class UserUnbannedEvent
{
    public Guid UnbanId { get; set; }
    public Guid UserId { get; set; }
    public DateTime UnbannedAt { get; set; }
}