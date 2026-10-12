using Bizcord.Shared.Events;
using ProfileMicroservice.Handlers;
using ProfileMicroservice.Messaging;
using ProfileMicroservice.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IProfileService, ProfileService>();
builder.Services.AddMessageClient(builder.Configuration);
builder.Services.AddMessageHandler<MessagePostedEvent, MessagePostedHandler>("profile-microservice");
var app = builder.Build();

app.MapControllers();

app.Run();