using System.Text;
using System.Text.Json;
using AuthService.Events;
using RabbitMQ.Client;

namespace AuthService.Services;

public class RabbitMqPublisher : IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _initialized;

    public RabbitMqPublisher(IConfiguration config)
    {
        _config = config;
    }

    private async Task EnsureConnectedAsync()
    {
        if (_initialized && _connection != null && _channel != null && _connection.IsOpen && _channel.IsOpen)
            return;

        await _lock.WaitAsync();
        try
        {
            if (_initialized && _connection != null && _channel != null && _connection.IsOpen && _channel.IsOpen)
                return;

            var hostName = _config["RabbitMQ:HostName"] ?? throw new InvalidOperationException("RabbitMQ:HostName missing");
            var userName = _config["RabbitMQ:UserName"] ?? "guest";
            var password = _config["RabbitMQ:Password"] ?? "guest";

            var queueNameUserDeleted = _config["RabbitMQ:QueueNameUserDeleted"] ?? throw new InvalidOperationException("RabbitMQ:QueueNameUserDeleted missing");
            var queueNameUserContentDeleted = _config["RabbitMQ:QueueNameUserContentDeleted"] ?? throw new InvalidOperationException("RabbitMQ:QueueNameUserContentDeleted missing");

            var factory = new ConnectionFactory
            {
                HostName = hostName,
                UserName = userName,
                Password = password,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();
            
            await _channel.ExchangeDeclareAsync(exchange: "email-exchange", type: ExchangeType.Direct, durable: true, autoDelete: false);
            
            await _channel.QueueDeclareAsync(queue: queueNameUserDeleted, durable: true, exclusive: false, autoDelete: false);
            await _channel.QueueDeclareAsync(queue: queueNameUserContentDeleted, durable: true, exclusive: false, autoDelete: false);

            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PublishConfirmationEmailAsync(Guid userId, string email, string confirmationLink)
    {
        await EnsureConnectedAsync();
        
        var message = new 
        { 
            Recipient = email, 
            Url = confirmationLink,
            Id = userId
        };
        
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        
        await _channel!.BasicPublishAsync(exchange: "email-exchange", routingKey: "confirm-email", body: body);
    }

    public async Task PublishUserDeletedAsync(UserDeletedEvent deletedEvent)
    {
        await EnsureConnectedAsync();
        
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(deletedEvent));
        var queueName = _config["RabbitMQ:QueueNameUserDeleted"] ?? throw new InvalidOperationException("RabbitMQ:QueueNameUserDeleted missing");
        
        await _channel!.BasicPublishAsync(exchange: "", routingKey: queueName, body: body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel != null)
            await _channel.CloseAsync();

        if (_connection != null)
            await _connection.CloseAsync();
            
        _lock.Dispose();
    }
}