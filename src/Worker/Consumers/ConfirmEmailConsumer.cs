using AuthService;
using MassTransit;
using Worker.Infrastructure.Email;
using Worker.Messages;

namespace Worker.Consumers;

public class ConfirmEmailConsumer(IEmailSender emailSender, AuthGrpcService.AuthGrpcServiceClient authClient) : MassTransit.IConsumer<ConfirmEmailMessage>
{

    public async Task Consume(ConsumeContext<ConfirmEmailMessage> context)
    {
        var message = context.Message;
        
        var userResponse = await authClient.GetUserEmailAsync(new UserRequest { 
            UserId = message.Id.ToString()
        });
        
        
        var emailTo = string.IsNullOrEmpty(userResponse.Email)
            ? message.Recipient
            : userResponse.Email;
        
        string subject = "Добро пожаловать в Explain Hub! Подтвердите вашу почту";

        string body = $@"<h2>Добро пожаловать в ExplainHub!</h2>
            <p>Для завершения регистрации, пожалуйста, перейдите по ссылке ниже:</p>
            <p>
                <a href='{message.Url}'>Нажмите сюда, чтобы подтвердить регистрацию</a>
            </p>
            <p style='color: gray; font-size: 12px;'>
                Если ссылка не работает, скопируйте этот адрес в браузер: {message.Url}
            </p>";
        
        await emailSender.SendAsync(emailTo, subject, body, context.CancellationToken);
    }
}