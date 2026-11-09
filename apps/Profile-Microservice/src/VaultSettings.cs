namespace ProfileMicroservice;

public class VaultSettings
{
    public string Address { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string MountPath { get; set; } = "secret";
    public string SecretPath { get; set; } = "bizcord/profile";
}