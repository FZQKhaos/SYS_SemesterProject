using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt section is missing.");

if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
}

var clients = builder.Configuration.GetSection("Clients")
    .Get<Dictionary<string, ClientConfig>>() ?? new Dictionary<string, ClientConfig>();

var app = builder.Build();

var credentials = new SigningCredentials(
    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
    SecurityAlgorithms.HmacSha256);
var handler = new JsonWebTokenHandler();

app.MapPost("/connect/token", (TokenRequest request) =>
{
    var clientId = request.ClientId;

    if (string.IsNullOrWhiteSpace(clientId)
        || !clients.TryGetValue(clientId, out var client)
        || !SecretMatches(client.Secret, request.ClientSecret))
    {
        return Results.Unauthorized();
    }

    var token = handler.CreateToken(new SecurityTokenDescriptor
    {
        Issuer = jwt.Issuer,
        Audience = jwt.Audience,
        Subject = new ClaimsIdentity(new[]
        {
            new Claim("sub", clientId),
            new Claim("role", client.Role)
        }),
        Expires = DateTime.UtcNow.AddMinutes(jwt.LifetimeMinutes),
        SigningCredentials = credentials
    });

    return Results.Ok(new TokenResponse(token, "Bearer", jwt.LifetimeMinutes * 60));
});

app.Run();

static bool SecretMatches(string expected, string? provided)
{
    if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
    {
        return false;
    }

    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected),
        Encoding.UTF8.GetBytes(provided));
}

record TokenRequest(string? ClientId, string? ClientSecret);

record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);

class JwtSettings
{
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "bizcord-auth";
    public string Audience { get; set; } = "internal-api";
    public int LifetimeMinutes { get; set; } = 5;
}

class ClientConfig
{
    public string Secret { get; set; } = "";
    public string Role { get; set; } = "";
}