using MassTransit;
using Microsoft.Extensions.Options;

namespace QnaService.Services;

public class RabbitMqLikePublisher
{
    private readonly RabbitMqSettings _settings;
    private readonly ISendEndpointProvider _sendEndpointProvider;

    public RabbitMqLikePublisher(IOptions<RabbitMqSettings> options, ISendEndpointProvider sendEndpointProvider)
    {
        _settings = options.Value;
        _sendEndpointProvider = sendEndpointProvider;
    }

    public async Task PublishAsync(LikeNotificationMessage message, CancellationToken ct)
    {
        var endpoint = await _sendEndpointProvider.GetSendEndpoint(new Uri($"exchange:{_settings.ExchangeName}?type=direct"));
        await endpoint.Send(message, context => context.SetRoutingKey(_settings.LikeRoutingKey), ct);
    }
}
