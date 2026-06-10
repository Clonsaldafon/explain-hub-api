namespace QnaService.Services;

public class RabbitMqSettings
{
    public string HostName { get; set; } = "localhost";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string ExchangeName { get; set; } = "email-exchange";
    public string LikeRoutingKey { get; set; } = "like-notification";
}
