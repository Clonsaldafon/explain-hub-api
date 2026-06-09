using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Worker.Consumers;
using Worker.Messages;


namespace Worker.Infrastructure.Messaging;

public class RabbitMqListener(IOptions<RabbitMqConfiguration> options, IServiceProvider serviceProvider) : BackgroundService
{
    protected async override Task ExecuteAsync(CancellationToken ct)
    {
        var configuration = options.Value;
        
        var factory = new ConnectionFactory()
        {
            HostName = configuration.HostName,
            Port = configuration.Port,
            UserName = configuration.UserName,
            Password = configuration.Password
        };

        using var connection = await factory.CreateConnectionAsync(ct);
        using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        
        await InitializeRabbitMqAsync(channel, configuration, ct);
        
        var consumer = new AsyncEventingBasicConsumer(channel);
        
        consumer.ReceivedAsync += async (model, ea) => await HandleMessageReceivedAsync(channel, ea, ct);
        
        await channel.BasicConsumeAsync(queue: configuration.QueueName, autoAck: false, consumer: consumer, cancellationToken: ct);
        
        await Task.Delay(Timeout.Infinite, ct);
    }

    private async Task InitializeRabbitMqAsync(IChannel channel, RabbitMqConfiguration config, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(exchange: config.ExchangeName, type: ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: ct);
        await channel.QueueDeclareAsync(queue: config.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct);
        await channel.QueueBindAsync(queue: config.QueueName, exchange: config.ExchangeName, routingKey: "confirm-email", cancellationToken: ct);
        await channel.QueueBindAsync(queue: config.QueueName, exchange: config.ExchangeName, routingKey: "like-notification", cancellationToken: ct);
    }
    
    private async Task HandleMessageReceivedAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var body = ea.Body.ToArray();
        var messageJson = Encoding.UTF8.GetString(body);
        var routingKey = ea.RoutingKey;

        try
        {
            using var scope = serviceProvider.CreateScope();

            if (routingKey == "confirm-email")
            {
                var message = JsonSerializer.Deserialize<ConfirmEmailMessage>(messageJson);
                if (message != null)
                {
                    var mailConsumer = scope.ServiceProvider.GetRequiredService<IConsumer<ConfirmEmailMessage>>();
                    await mailConsumer.ConsumeAsync(message, ct);
                }
            }
            else if (routingKey == "like-notification")
            {
                var message = JsonSerializer.Deserialize<LikeNotificationMessage>(messageJson);
                if (message != null)
                {
                    var likeConsumer = scope.ServiceProvider.GetRequiredService<IConsumer<LikeNotificationMessage>>();
                    await likeConsumer.ConsumeAsync(message, ct);
                }
            }
            
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Ошибка] Причина: {ex.GetType().Name} - {ex.Message}");
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
        }
    }
}