using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Worker.Infrastructure.Email;

public class SmtpEmailSender(IOptions<SmtpSettings> settings) : IEmailSender
{
    private readonly SmtpSettings _settings = settings.Value;

    public async Task SendAsync(string email, string subject, string message, CancellationToken ct)
    {
        try
        {
            var emailMessage = new MimeMessage();
            
            emailMessage.From.Add(new MailboxAddress(_settings.SenderName, _settings.Username));
            
            emailMessage.To.Add(new MailboxAddress("", email));
            emailMessage.Subject = subject;
            
            var bodyBuilder = new BodyBuilder { HtmlBody = message };
            emailMessage.Body = bodyBuilder.ToMessageBody();

            using var smtpClient = new SmtpClient();
            
            var socketOption = _settings.Port == 465 
                ? SecureSocketOptions.SslOnConnect 
                : SecureSocketOptions.StartTls;

            await smtpClient.ConnectAsync(_settings.Host, _settings.Port, socketOption, ct);
            await smtpClient.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            await smtpClient.SendAsync(emailMessage, ct);
            await smtpClient.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            throw new Exception($"SMTP Error: {ex.GetType().Name} - {ex.Message}", ex);
        }
    }
}