using ProfileMicroservice.Messaging;
using ProfileMicroservice.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IProfileService, ProfileService>();
builder.Services.AddMessageClient(builder.Configuration);

var app = builder.Build();

app.MapControllers();

app.Run();