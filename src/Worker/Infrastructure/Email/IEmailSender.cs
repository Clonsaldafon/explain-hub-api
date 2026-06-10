using Worker.Messages;

namespace Worker.Infrastructure.Email;

public interface IEmailSender
{
    Task SendAsync(string email,  string subject, string message, CancellationToken ct);
}