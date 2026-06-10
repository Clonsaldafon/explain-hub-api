using System.ComponentModel.DataAnnotations.Schema;

namespace QnaService.Models;

[Table("question_views")]
public class QuestionView
{
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("question_id")]
    public Guid QuestionId { get; set; }

    [Column("viewer_id")]
    public Guid? ViewerId { get; set; }

    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Question Question { get; set; } = null!;
}
