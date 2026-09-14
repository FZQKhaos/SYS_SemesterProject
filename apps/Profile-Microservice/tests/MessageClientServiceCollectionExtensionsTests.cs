using EasyNetQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YourService.Messaging;

namespace tests;

public class MessageClientServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMessageClient_WhenConnectionStringIsMissing_ThrowsBeforeRegisteringServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddMessageClient(configuration));

        Assert.Equal("RabbitMQ:ConnectionString is missing.", exception.Message);
        Assert.Empty(services);
    }

    [Fact]
    public void AddMessageClient_WithConnectionString_RegistersMessagingAndReturnsSameCollection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:ConnectionString"] = "host=localhost"
            })
            .Build();

        var result = services.AddMessageClient(configuration);

        Assert.Same(services, result);
        var client = Assert.Single(services, entry => entry.ServiceType == typeof(IMessageClient));
        Assert.Equal(typeof(MessageClient), client.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, client.Lifetime);
        Assert.Contains(services, entry => entry.ServiceType == typeof(IBus));
    }
}
