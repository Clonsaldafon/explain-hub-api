using AuthService.Events;
using MassTransit;

namespace AuthService.Services;

public class RabbitMqPublisher
{
    private readonly IConfiguration _config;
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public RabbitMqPublisher(IConfiguration config, ISendEndpointProvider sendEndpointProvider)
    {
        _config = config;
        _sendEndpointProvider = sendEndpointProvider;
    }

    public async Task PublishConfirmationEmailAsync(Guid userId, string email, string confirmationLink)
    {
        var message = new ConfirmEmailMessage
        {
            Recipient = email,
            Url = confirmationLink,
            Id = userId
        };

        var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri("exchange:email-exchange?type=direct"));
        await endpoint.Send(message, context => context.SetRoutingKey("confirm-email"));
    }

    public async Task PublishUserDeletedAsync(UserDeletedEvent deletedEvent)
    {
        var queueName = _config["RabbitMQ:QueueNameUserDeleted"] ?? throw new InvalidOperationException("RabbitMQ:QueueNameUserDeleted missing");
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri($"queue:{queueName}"));
        await endpoint.Send(deletedEvent);
    }
}

public class ConfirmEmailMessage
{
    public string Recipient { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
}
