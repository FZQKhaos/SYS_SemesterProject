using Rebus.Sagas;

namespace ProfileMicroservice.Sagas;

public class BanSagaData : ISagaData
{
    // Disse to kræver Rebus. Rebus bruger dem selv internt.
    public Guid Id { get; set; }
    public int Revision { get; set; }

    // Correlation: det her id matcher beskederne til den rigtige saga.
    public Guid BanId { get; set; }
    public Guid UserId { get; set; }

    // Ét flag pr. trin, ligesom i opgaven.
    public bool MembershipSuspended { get; set; }
    public bool CallTerminated { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsFailed { get; set; }
    public string? FailureReason { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}