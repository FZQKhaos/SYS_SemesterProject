using EasyNetQ;

namespace YourService.Messaging;

public static class MessageClientServiceCollectionExtensions
{
    public static IServiceCollection AddMessageClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration["RabbitMQ:ConnectionString"]
            ?? throw new InvalidOperationException(
                "RabbitMQ:ConnectionString is missing.");

        services.AddEasyNetQ(connectionString);

        services.AddSingleton<IMessageClient, EasyNetQMessageClient>();

        return services;
    }
}

/*
Johan Noter!

AddMessageClient:
Extension method der samler hele opsætningen af vores message client ét sted.
Program.cs behøver derfor ikke kende detaljerne omkring EasyNetQ.

RabbitMQ:ConnectionString:
Hentes fra configuration.
Det betyder at connection string kan komme fra appsettings.json,
environment variables eller anden .NET configuration.

AddEasyNetQ:
Registrerer EasyNetQ og dens IBus i dependency injection containeren.

AddSingleton<IMessageClient, EasyNetQMessageClient>:
Når resten af programmet beder om IMessageClient,
giver dependency injection vores EasyNetQ implementation tilbage.

Fordelen:
RabbitMQ/EasyNetQ configuration er samlet her i stedet for at være
spredt rundt omkring i microservicen.
*/