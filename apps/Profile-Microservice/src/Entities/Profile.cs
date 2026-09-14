namespace ProfileMicroservice.Entities;

public class Profile
{
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string? DisplayName { get; set; }
    public List<Friend> Friends { get; set; } = new();
}
