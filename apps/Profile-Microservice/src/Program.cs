using YourService.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMessageClient(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new
{
    Service = "REPLACE-WITH-DOMAIN-microservice",
    Status = "Running"
}));

app.Run();

/*
Johan Noter!

AddMessageClient:
Registrerer vores IMessageClient og EasyNetQ implementation.
Program.cs ved kun at der findes en message client og behøver ikke kende
detaljerne omkring RabbitMQ.

MapGet("/"):
Et simpelt endpoint så vi kan se at microservicen kører.

Der er ikke lavet publish eller subscribe direkte i Program.cs.
Det skal senere gøres i de services/handlers der faktisk har brug for messaging.
*/