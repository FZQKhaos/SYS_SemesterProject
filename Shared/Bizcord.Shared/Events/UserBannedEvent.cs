namespace Bizcord.Shared.Events;

public class UserBannedEvent
{
    public Guid BanId { get; set; }
    public Guid UserId { get; set; }
    public DateTime BannedAt { get; set; }
}