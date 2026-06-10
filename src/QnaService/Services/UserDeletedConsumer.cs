using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QnaService.Data;
using QnaService.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace QnaService.Services;

public class UserDeletedConsumer(IServiceProvider services, IConfiguration config) : BackgroundService
{
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _initialized;


    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_initialized && _connection != null && _channel != null && _connection.IsOpen && _channel.IsOpen)
            return;

        var hostName = config["RabbitMQ:HostName"] ?? "rabbitmq";
        var userName = config["RabbitMQ:UserName"] ?? "guest";
        var password = config["RabbitMQ:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = await factory.CreateConnectionAsync(ct);
        _channel = await _connection.CreateChannelAsync(cancellationToken: ct);
        
        await _channel.QueueDeclareAsync("user.deleted", true, false, false, cancellationToken: ct);
        await _channel.QueueDeclareAsync("user_content_deleted", true, false, false, cancellationToken: ct);

        _initialized = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConnectedAsync(stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            await HandleMessageAsync(ea, stoppingToken);
        };

        await _channel!.BasicConsumeAsync("user.deleted", false, consumer, stoppingToken);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        try
        {
            var body = ea.Body.ToArray();
            var message = JsonSerializer.Deserialize<UserDeletedEvent>(Encoding.UTF8.GetString(body));

            if (message != null)
            {
                var (success, error) = await ProcessUserDeletionAsync(message.UserId, ct);
                
                await SendResponseAsync(message.UserId, success, error, ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Critical error in UserDeletedConsumer: {ex.Message}");
        }
        finally
        {
            if (_channel != null && _channel.IsOpen)
            {
                await _channel.BasicAckAsync(ea.DeliveryTag, false, ct);
            }
        }
    }

    private async Task<(bool Success, string? Error)> ProcessUserDeletionAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<QnaDbContext>();
            
            await db.Questions
                .Where(q => q.AuthorId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true), ct);
            
            await db.Answers
                .Where(a => a.AuthorId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true), ct);

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task SendResponseAsync(Guid userId, bool success, string? error, CancellationToken ct)
    {
        var responseEvent = new UserContentDeletedEvent
        {
            UserId = userId,
            Success = success,
            Error = error
        };

        var responseBody = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(responseEvent));
        
        await _channel!.BasicPublishAsync(
            exchange: "",
            routingKey: "user_content_deleted",
            body: responseBody,
            cancellationToken: ct);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}