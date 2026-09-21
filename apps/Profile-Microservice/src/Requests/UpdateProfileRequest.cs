namespace ProfileMicroservice.Requests;

public sealed record UpdateProfileRequest
{
    public string? DisplayName { get; init; }
}