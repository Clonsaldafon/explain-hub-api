namespace Worker.Messages;

public class ConfirmEmailMessage : BaseMessage
{
    public string Recipient { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}