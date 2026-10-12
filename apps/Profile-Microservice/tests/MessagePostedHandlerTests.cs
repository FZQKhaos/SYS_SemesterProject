using Bizcord.Shared.Events;
using ProfileMicroservice.Handlers;
using ProfileMicroservice.Services;

namespace tests;

public class MessagePostedHandlerTests
{
    private readonly ProfileService _profiles = new();
    private readonly FakeMessageClient _client = new();
    private readonly MessagePostedHandler _handler;

    public MessagePostedHandlerTests()
    {
        _handler = new MessagePostedHandler(_profiles, _client);
    }

    private static MessagePostedEvent MessageFrom(Guid authorId, Guid messageId)
    {
        return new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = authorId,
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Handle_PublishesResultEvent_WithCorrectMessageId()
    {
        // Arrange
        var profile = _profiles.Create("Yahya");
        var messageId = Guid.NewGuid();
        var message = MessageFrom(Guid.Parse(profile.Id), messageId);

        // Act
        await _handler.Handle(message, CancellationToken.None);

        // Assert
        var published = _client.SinglePublished<ProfileActivityUpdatedEvent>();
        Assert.NotNull(published);
        Assert.Equal(messageId, published.MessageId);
        Assert.Equal(profile.Id, published.ProfileId);

        var stored = _profiles.GetById(profile.Id);
        Assert.NotNull(stored);
        Assert.NotNull(stored.LastMessageAt);
    }

    [Fact]
    public async Task Handle_UnknownAuthor_PublishesNothing()
    {
        // Arrange
        var unknownAuthorId = Guid.NewGuid();
        var message = MessageFrom(unknownAuthorId, Guid.NewGuid());

        // Act
        await _handler.Handle(message, CancellationToken.None);

        // Assert
        Assert.Empty(_client.Published);
    }

    [Fact]
    public async Task Handler_CanConsume_MinimumValidContract()
    {
        // Arrange
        var profile = _profiles.Create("Yahya");
        var message = MessageFrom(Guid.Parse(profile.Id), Guid.NewGuid());

        // Act
        var exception = await Record.ExceptionAsync(
            () => _handler.Handle(message, CancellationToken.None));

        // Assert
        Assert.Null(exception);
        Assert.Single(_client.Published);
    }
}