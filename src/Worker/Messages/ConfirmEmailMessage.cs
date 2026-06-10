namespace Worker.Messages;

public class ConfirmEmailMessage : BaseMessage
{
    public string Recipient { get; set; } = string.Empty;
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
}