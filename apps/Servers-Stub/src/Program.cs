using EasyNetQ;
using ServersStub;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration["RabbitMQ:ConnectionString"]
                       ?? "host=localhost;username=profileapp;password=profileapp";

builder.Services.AddEasyNetQ(connectionString);
builder.Services.AddHostedService<ServersWorker>();
builder.Services.AddHostedService<VoiceWorker>();
builder.Services.AddHostedService<MessagingWorker>();
builder.Services.AddHostedService<SessionWorker>();

builder.Build().Run();