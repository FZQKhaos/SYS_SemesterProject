namespace Bizcord.Shared.Models;

public sealed record ProfileDto
{
    public string Id { get; init; } = string.Empty;
    public DateTime? CreatedAt { get; init; }
    public string? DisplayName { get; init; }
}