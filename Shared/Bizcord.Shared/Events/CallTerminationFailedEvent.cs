namespace Bizcord.Shared.Events;

public class CallTerminationFailedEvent
{
    public Guid BanId { get; set; }
    public Guid UserId { get; set; }
    public int Attempts { get; set; }
    public required string Reason { get; set; }
}