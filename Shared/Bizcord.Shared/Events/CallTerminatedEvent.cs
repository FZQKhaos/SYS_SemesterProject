namespace Bizcord.Shared.Events;

public class CallTerminatedEvent
{
    public Guid BanId { get; set; }
    public Guid UserId { get; set; }
    public DateTime TerminatedAt { get; set; }
}