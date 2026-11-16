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
builder.Services.AddMessageHandler<MembershipSuspensionFailedEvent, MembershipSuspensionFailedHandler>("profile-microservice");
builder.Services.AddMessageHandler<UserBannedEvent, BanStartedTrackingHandler>("profile-microservice");
builder.Services.AddMessageHandler<MembershipSuspendedEvent, BanSuspendedTrackingHandler>("profile-microservice");
builder.Services.AddMessageHandler<CallTerminatedEvent, BanCompletedTrackingHandler>("profile-microservice");
builder.Services.AddMessageHandler<CallTerminationFailedEvent, BanNeedsInterventionTrackingHandler>("profile-microservice");
var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program { }