using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// Læs JWT-indstillinger. Skal være identiske med Auth-Service.
string signingKey = builder.Configuration["Jwt:SigningKey"]
                    ?? throw new InvalidOperationException("Jwt:SigningKey mangler (env Jwt__SigningKey).");
string issuer = builder.Configuration["Jwt:Issuer"]
                ?? throw new InvalidOperationException("Jwt:Issuer mangler (env Jwt__Issuer).");
string audience = builder.Configuration["Jwt:Audience"]
                  ?? throw new InvalidOperationException("Jwt:Audience mangler (env Jwt__Audience).");

if (Encoding.UTF8.GetByteCount(signingKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 bytes.");
}

// "Bearer" er navnet på denne login-metode. Ocelot henviser til det navn i ocelot.json.
builder.Services
    .AddAuthentication()
    .AddJwtBearer("Bearer", options =>
    {
        options.MapInboundClaims = false; // behold claim-navnene som "role" og "sub"
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RoleClaimType = "role",
            NameClaimType = "sub",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

await app.UseOcelot();
await app.RunAsync();