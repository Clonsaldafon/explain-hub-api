namespace Worker.Infrastructure.Messaging;

public class RabbitMqConfiguration
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string QueueName { get; set; } = "default";
    public string ExchangeName { get; set; } = "default";
}