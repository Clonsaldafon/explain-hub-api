using Worker.Messages;

namespace Worker.Consumers;

public interface IConsumer<TMessage> where TMessage : BaseMessage
{
    Task ConsumeAsync(TMessage message, CancellationToken ct);
}