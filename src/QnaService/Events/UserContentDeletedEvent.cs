namespace QnaService.Events;

public class UserContentDeletedEvent
{
    public Guid UserId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}