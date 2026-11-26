using Bizcord.Shared.Commands;
using Bizcord.Shared.Events;
using ProfileMicroservice.Sagas;
using Rebus.Config;
using Rebus.Persistence.InMem;
using Rebus.Routing.TypeBased;

namespace ProfileMicroservice.Messaging;

public static class RebusServiceCollectionExtensions
{
    public static IServiceCollection AddRebusSaga(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var host = configuration["RABBITMQ_HOST"] ?? "localhost";
        var user = configuration["RABBITMQ_USER"] ?? "profileapp";
        var password = configuration["RABBITMQ_PASSWORD"] ?? "profileapp";

        var connectionString =
            $"amqp://{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(password)}@{host}";

        var servicesBefore = services.Count;

        services.AddRebus(
            configure => configure
                .Transport(t => t.UseRabbitMq(connectionString, "profile-saga"))
                .Routing(r => r.TypeBased()
                    .Map<SuspendMembership>("servers-stub")
                    .Map<TerminateCall>("servers-stub"))
                .Sagas(s => s.StoreInMemory()),
            onCreated: async bus =>
            {
                await bus.Subscribe<UserBannedEvent>();
                await bus.Subscribe<MembershipSuspendedEvent>();
                await bus.Subscribe<MembershipSuspensionFailedEvent>();
                await bus.Subscribe<CallTerminatedEvent>();
                await bus.Subscribe<CallTerminationFailedEvent>();
            });

        // AddRebus registrerer en hosted service, der opretter bussen, mens værten starter.
        // Er RabbitMQ nede, kaster den, og så starter hele servicen ikke. Vi fjerner den
        // og starter bussen i stedet i RebusStartupService, med genforsøg og i baggrunden.
        // Kun det, AddRebus lige har tilføjet (index servicesBefore og op), bliver fjernet.
        for (var i = services.Count - 1; i >= servicesBefore; i--)
        {
            if (services[i].ServiceType == typeof(IHostedService))
            {
                services.RemoveAt(i);
            }
        }

        services.AddRebusHandler<BanSaga>();

        services.AddSingleton(new RebusBrokerSettings(connectionString));
        services.AddSingleton<RebusReadiness>();
        services.AddHostedService<RebusStartupService>();

        return services;
    }
}