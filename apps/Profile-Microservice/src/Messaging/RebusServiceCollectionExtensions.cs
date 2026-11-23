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

        services.AddRebusHandler<BanSaga>();

        return services;
    }
}