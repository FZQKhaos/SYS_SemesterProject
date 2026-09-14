namespace ProfileMicroservice.Entities;

public class Friend
{
    public string Id { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
    public string? DisplayName { get; set; }
}
