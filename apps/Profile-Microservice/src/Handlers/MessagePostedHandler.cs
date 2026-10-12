using Bizcord.Shared.Events;
using ProfileMicroservice.Messaging;
using ProfileMicroservice.Services;

namespace ProfileMicroservice.Handlers;

public sealed class MessagePostedHandler : IMessageHandler<MessagePostedEvent>
{
    private readonly IProfileService _profiles;
    private readonly IMessageClient _messageClient;

    public MessagePostedHandler(IProfileService profiles, IMessageClient messageClient)
    {
        _profiles = profiles;
        _messageClient = messageClient;
    }

    public async Task Handle(MessagePostedEvent message, CancellationToken cancellationToken)
    {
        var profile = _profiles.RecordMessage(message.AuthorId.ToString(), message.PostedAt);

        if (profile is null)
        {
            return;
        }

        await _messageClient.PublishAsync(
            new ProfileActivityUpdatedEvent
            {
                MessageId = message.MessageId,
                ProfileId = profile.Id,
                LastMessageAt = profile.LastMessageAt ?? message.PostedAt,
                ProcessedAt = DateTime.UtcNow
            },
            cancellationToken);
    }
}