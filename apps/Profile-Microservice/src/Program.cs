using Bizcord.Shared.Events;
using Newtonsoft.Json.Linq;
using ProfileMicroservice;
using ProfileMicroservice.Handlers;
using ProfileMicroservice.Messaging;
using ProfileMicroservice.Services;
using Vault;
using Vault.Client;
using Vault.Model;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// NYT: 1. Læs VaultSettings fra konfigurationen (sektionen "Vault")
var vaultSettings = builder.Configuration.GetSection("Vault").Get<VaultSettings>()
    ?? new VaultSettings();

// NYT: 2. Er Vault sat op? Så hent secrets derfra
if (!string.IsNullOrWhiteSpace(vaultSettings.Address))
{
    SecretSettings secretSettings;

    try
    {
        var vaultConfig = new VaultConfiguration(vaultSettings.Address);
        var vaultClient = new VaultClient(vaultConfig);

        var authResponse = vaultClient.Auth.UserpassLogin(
            vaultSettings.Username,
            new UserpassLoginRequest(vaultSettings.Password));
        vaultClient.SetToken(authResponse.ResponseAuth.ClientToken);

        VaultResponse<KvV2ReadResponse> response = vaultClient.Secrets.KvV2Read(
            vaultSettings.SecretPath,
            kvV2MountPath: vaultSettings.MountPath);
        
        JObject data = (JObject)response.Data.Data;
        secretSettings = data.ToObject<SecretSettings>()
            ?? throw new InvalidOperationException("Vault secret could not be read.");
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"Could not read '{vaultSettings.SecretPath}' from Vault at {vaultSettings.Address}: {ex.Message}", ex);
    }

    if (string.IsNullOrWhiteSpace(secretSettings.RabbitMqPassword))
    {
        throw new InvalidOperationException("Vault secret is missing the key 'RabbitMqPassword'.");
    }

    // NYT: 3. Læg passwordet i konfigurationen, så AddMessageClient finder det som før
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["RABBITMQ_PASSWORD"] = secretSettings.RabbitMqPassword
    });

    builder.Services.AddSingleton(secretSettings);
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// NYT: JWT-validering, så servicen selv kontrollerer tokens (defense in depth)
string jwtKey = builder.Configuration["Jwt:SigningKey"]
                ?? throw new InvalidOperationException("Jwt:SigningKey mangler (env Jwt__SigningKey).");
string jwtIssuer = builder.Configuration["Jwt:Issuer"]
                   ?? throw new InvalidOperationException("Jwt:Issuer mangler (env Jwt__Issuer).");
string jwtAudience = builder.Configuration["Jwt:Audience"]
                     ?? throw new InvalidOperationException("Jwt:Audience mangler (env Jwt__Audience).");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
}

builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            RoleClaimType = "role",
            NameClaimType = "sub",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// En "policy" er en navngivet regel. Denne kræver login og role = internal-service.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("InternalService", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("role", "internal-service"));

builder.Services.AddSingleton<IProfileService, ProfileService>();
builder.Services.AddMessageClient(builder.Configuration);
builder.Services.AddMessageHandler<MessagePostedEvent, MessagePostedHandler>("profile-microservice");
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }