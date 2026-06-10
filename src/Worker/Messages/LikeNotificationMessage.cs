namespace Worker.Messages;

public class LikeNotificationMessage : BaseMessage
{
    public string Url { get; set; } = string.Empty;
    public string Recipient { get; set; } =  string.Empty;
    public Guid Id { get; set; }
    public string LikerName { get; set; } = string.Empty;
    public string PostTitle { get; set; } = string.Empty;
}