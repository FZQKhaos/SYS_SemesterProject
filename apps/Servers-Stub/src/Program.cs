using Bizcord.Shared.Events;
using EasyNetQ;
using Rebus.Config;
using ServersStub;
using ServersStub.Handlers;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration["RabbitMQ:ConnectionString"]
                       ?? "host=localhost;username=profileapp;password=profileapp";

builder.Services.AddEasyNetQ(connectionString);

var rabbitHost = builder.Configuration["RABBITMQ_HOST"] ?? "localhost";
var rabbitUser = builder.Configuration["RABBITMQ_USER"] ?? "profileapp";
var rabbitPassword = builder.Configuration["RABBITMQ_PASSWORD"] ?? "profileapp";

var rebusConnectionString =
    $"amqp://{Uri.EscapeDataString(rabbitUser)}:{Uri.EscapeDataString(rabbitPassword)}@{rabbitHost}";

builder.Services.AddSingleton<SystemMessageStore>();

builder.Services.AddRebus(
    configure => configure
        .Transport(t => t.UseRabbitMq(rebusConnectionString, "servers-stub")),
    onCreated: async bus =>
    {
        await bus.Subscribe<UserBannedEvent>();
        await bus.Subscribe<MembershipSuspensionFailedEvent>();
        await bus.Subscribe<MembershipSuspendedEvent>();
    });

builder.Services.AddRebusHandler<SuspendMembershipHandler>();
builder.Services.AddRebusHandler<TerminateCallHandler>();
builder.Services.AddRebusHandler<SessionsBanHandler>();
builder.Services.AddRebusHandler<PostSystemMessageHandler>();

builder.Services.AddHostedService<ServersWorker>();
builder.Services.AddHostedService<VoiceWorker>();
builder.Services.AddHostedService<MessagingWorker>();
builder.Services.AddHostedService<SessionWorker>();

builder.Build().Run();