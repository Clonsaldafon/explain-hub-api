using AuthService;
using MassTransit;
using Worker.Infrastructure.Email;
using Worker.Messages;

namespace Worker.Consumers;

public class LikeNotificationConsumer(IEmailSender emailSender, AuthGrpcService.AuthGrpcServiceClient authClient) : MassTransit.IConsumer<LikeNotificationMessage>
{
    
    public async Task Consume(ConsumeContext<LikeNotificationMessage> context)
    {
        var message = context.Message;
        var targetName = string.Equals(message.TargetType, "question", StringComparison.OrdinalIgnoreCase)
            ? "вопрос"
            : "ответ";

        string subject = $"Ваш {targetName} получил лайк";
        
        var userResponse = await authClient.GetUserEmailAsync(new UserRequest { 
            UserId = message.Id.ToString()
        });
        
        string emailTo = string.IsNullOrEmpty(userResponse.Email)
            ? message.Recipient
            : userResponse.Email;
        
        string subject = $"Вас лайкнули под постом {message.PostTitle}";

        string body = $@"<div style='font-family: Arial, sans-serif; padding: 20px; text-align: center;'>
                <h2>Пользователю {message.LikerName} понравился ваш {targetName}: {message.PostTitle}</h2>
                <p style='margin-bottom: 20px;'>Перейдите по ссылке, чтобы посмотреть:</p>
                <a href='{message.Url}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                    Открыть
                </a>
                </div>";

        await emailSender.SendAsync(emailTo, subject, body, context.CancellationToken);
    }
}