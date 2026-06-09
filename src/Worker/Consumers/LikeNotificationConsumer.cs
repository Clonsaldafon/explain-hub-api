using Worker.Infrastructure.Email;
using Worker.Messages;

namespace Worker.Consumers;

public class LikeNotificationConsumer(IEmailSender emailSender) : IConsumer<LikeNotificationMessage>
{
    public async Task ConsumeAsync(LikeNotificationMessage message, CancellationToken ct)
    {
        string subject = $"Вас лайкнули под постом {message.PostTitle}";

        string body = $@"<div style='font-family: Arial, sans-serif; padding: 20px; text-align: center;'>
                <h2>Ваш ответ к посту {message.PostTitle} понравился пользователю {message.LikerName}!</h2>
                <p style='margin-bottom: 20px;'>Перейдите по ссылке, чтобы посмотреть ответ:</p>
                <a href='{message.Url}' style='display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; font-weight: bold;'>
                    Перейти к посту
                </a>
                </div>";
        
        await emailSender.SendAsync(message.Recipient, subject, body, ct);
    }
}