namespace QnaService.Services;

public class LikeNotificationMessage
{
    public Guid MessageId { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Url { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string LikerName { get; set; } = string.Empty;
    public string PostTitle { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
}
