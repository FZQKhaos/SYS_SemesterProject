namespace ProfileMicroservice.Messaging;

public interface IMessageHandler<TMessage>
{
    Task Handle(TMessage message, CancellationToken cancellationToken);
}