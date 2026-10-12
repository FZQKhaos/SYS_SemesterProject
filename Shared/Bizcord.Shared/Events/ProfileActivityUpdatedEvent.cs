namespace Bizcord.Shared.Events;

public class ProfileActivityUpdatedEvent
{
    public Guid MessageId { get; set; }
    public string ProfileId { get; set; } = string.Empty;
    public DateTime LastMessageAt { get; set; }
    public DateTime ProcessedAt { get; set; }
}