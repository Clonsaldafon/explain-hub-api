using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QnaService.Models;

[Table("answer_likes")]
public class AnswerLike
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("answer_id")]
    public Guid AnswerId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Required]
    [EmailAddress]
    [Column("user_email")]
    public string UserEmail { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Answer Answer { get; set; } = null!;
}
