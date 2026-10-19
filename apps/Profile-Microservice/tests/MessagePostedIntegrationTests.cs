using Bizcord.Shared.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ProfileMicroservice.Messaging;
using ProfileMicroservice.Services;

namespace tests;

[Trait("Category", "Integration")]
public class MessagePostedIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public MessagePostedIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // docker-compose opretter brugeren "profileapp" og ikke "guest".
        Environment.SetEnvironmentVariable("RABBITMQ_HOST", "localhost");
        Environment.SetEnvironmentVariable("RABBITMQ_USER", "profileapp");
        Environment.SetEnvironmentVariable("RABBITMQ_PASSWORD", "profileapp");
        _factory = factory;
    }

    [Fact]
    public async Task MessagePostedEvent_IsConsumed_AndResultEventPublished()
    {
        // Arrange
        _ = _factory.CreateClient(); // starter programmet, inkl. handlerens abonnement

        var profiles = _factory.Services.GetRequiredService<IProfileService>();
        var client = _factory.Services.GetRequiredService<IMessageClient>();
        var profile = profiles.Create("Integration");
        var messageId = Guid.NewGuid();

        var received = new TaskCompletionSource<ProfileActivityUpdatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var subscription = await client.SubscribeAsync<ProfileActivityUpdatedEvent>(
            $"test-{Guid.NewGuid()}",
            evt =>
            {
                if (evt.MessageId == messageId)
                {
                    received.TrySetResult(evt);
                }

                return Task.CompletedTask;
            });

        // Act
        await client.PublishAsync(new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.Parse(profile.Id),
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        });

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(messageId, result.MessageId);
        Assert.Equal(profile.Id, result.ProfileId);
    }
}