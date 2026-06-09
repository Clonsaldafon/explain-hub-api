using Worker.Infrastructure.Email;
using Worker.Messages;

namespace Worker.Consumers;

public class ConfirmEmailConsumer(IEmailSender emailSender) : IConsumer<ConfirmEmailMessage>
{
    public async Task ConsumeAsync(ConfirmEmailMessage message, CancellationToken ct)
    {
        string subject = "Добро пожаловать в Explain Hub! Подтвердите вашу почту";

        string body = $@"<h2>Добро пожаловать в ExplainHub!</h2>
            <p>Для завершения регистрации, пожалуйста, перейдите по ссылке ниже:</p>
            <p>
                <a href='{message.Url}'>Нажмите сюда, чтобы подтвердить регистрацию</a>
            </p>
            <p style='color: gray; font-size: 12px;'>
                Если ссылка не работает, скопируйте этот адрес в браузер: {message.Url}
            </p>";
        
        await emailSender.SendAsync(message.Recipient, subject, body, ct);
    }
}