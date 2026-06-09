using System.Text;
using System.Text.Json;
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
            var queueName = _config["RabbitMQ:QueueName"] ?? throw new InvalidOperationException("RabbitMQ:QueueName missing");

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
            await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);
            _initialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PublishConfirmationEmailAsync(string email, string confirmationLink)
    {
        int retryCount = 0;
        while (true)
        {
            try
            {
                await EnsureConnectedAsync();
                break;
            }
            catch (Exception ex) when (retryCount < 10)
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                Console.WriteLine($"RabbitMQ connection attempt {retryCount} failed: {ex.Message}. Retrying in {delay.TotalSeconds} sec...");
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to RabbitMQ after 10 attempts: {ex.Message}");
                throw;
            }
        }

        var message = new { Email = email, ConfirmationLink = confirmationLink };
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var queueName = _config["RabbitMQ:QueueName"]!;

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
