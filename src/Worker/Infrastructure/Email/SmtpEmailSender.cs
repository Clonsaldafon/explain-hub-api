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
            emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = message };

            using var smtpClient = new SmtpClient();
            await smtpClient.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.SslOnConnect, ct);
            
            await smtpClient.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            
            await smtpClient.SendAsync(emailMessage, ct);
            await smtpClient.DisconnectAsync(true, ct);
        }
        
        catch (Exception ex)
        {
            throw new Exception("Error occured while sending email message: " + ex.Message);
        }
    }
}