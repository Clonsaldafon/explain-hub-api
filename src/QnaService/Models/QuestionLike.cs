using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QnaService.Models;

[Table("question_likes")]
public class QuestionLike
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("question_id")]
    public Guid QuestionId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [EmailAddress]
    [Column("user_email")]
    public string UserEmail { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Question Question { get; set; } = null!;
}
