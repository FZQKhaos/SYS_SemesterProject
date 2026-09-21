namespace ProfileMicroservice.Requests
{
    public sealed record CreateProfileRequest
    {
        public string? DisplayName { get; init; }
    }
}
