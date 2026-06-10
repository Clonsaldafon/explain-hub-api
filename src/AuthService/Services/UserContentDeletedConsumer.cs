using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using AuthService.Events;
using AuthService.Data;

namespace AuthService.Services;

public class UserContentDeletedConsumer : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private IConnection? _connection;
    private IChannel? _channel;
    private string _queueName = null!;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
    private bool _initialized;

    public UserContentDeletedConsumer(IServiceProvider services, IConfiguration config)
    {
        _services = services;
        _config = config;
    }

    private async Task EnsureConnectedAsync()
    {
        if (_initialized && _connection != null && _channel != null && _connection.IsOpen && _channel.IsOpen)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized && _connection != null && _channel != null && _connection.IsOpen && _channel.IsOpen)
                return;

            var hostName = _config["RabbitMQ:HostName"] ?? throw new InvalidOperationException("RabbitMQ:HostName missing");
            var userName = _config["RabbitMQ:UserName"] ?? "guest";
            var password = _config["RabbitMQ:Password"] ?? "guest";

            _queueName = _config["RabbitMQ:QueueNameUserContentDeleted"] ?? throw new InvalidOperationException("RabbitMQ:QueueNameUserContentDeleted missing");

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

            await _channel.QueueDeclareAsync(_queueName, durable: true, exclusive: false, autoDelete: false);
            await _channel.BasicQosAsync(0, 1, false);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConnectedAsync();
        if (_channel == null) throw new InvalidOperationException("Channel not initialized");

        var consumer = new AsyncEventingBasicConsumer(_channel);
        
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = JsonSerializer.Deserialize<UserContentDeletedEvent>(Encoding.UTF8.GetString(body));

            try
            {
                if (message?.Success == true)
                {
                    using var scope = _services.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var user = await db.Users.FindAsync(new object[] { message.UserId }, cancellationToken: stoppingToken);
                    if (user != null)
                    {
                        db.Users.Remove(user);
                        await db.SaveChangesAsync(stoppingToken);
                        Console.WriteLine($"User {message.UserId} permanently deleted after content cleanup");
                    }
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                else
                {
                    Console.WriteLine($"Failed to delete content for user {message?.UserId}: {message?.Error}");
                    await _channel.BasicAckAsync(ea.DeliveryTag, false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing user content deleted event: {ex.Message}");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, false);
            }
        };

        await _channel.BasicConsumeAsync(_queueName, autoAck: false, consumer: consumer);

        try
        {
            await Task.Delay(-1, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine("UserContentDeletedConsumer is stopping due to cancellation");
        }
    }

    public override async void Dispose()
    {
        if (_channel != null)
            await _channel.CloseAsync();

        if (_connection != null)
            await _connection.CloseAsync();

        _initLock.Dispose();
        base.Dispose();
    }
}
