using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QnaService.Models;

[Table("answers")]
public class Answer
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("question_id")]
    public Guid QuestionId { get; set; }

    [Column("author_id")]
    public Guid AuthorId { get; set; }

    [Required]
    [EmailAddress]
    [Column("author_email")]
    public string AuthorEmail { get; set; } = string.Empty;

    [Required]
    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Column("like_count")]
    public int LikeCount { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public Question Question { get; set; } = null!;
    public ICollection<AnswerLike> Likes { get; set; } = new List<AnswerLike>();
    public ICollection<MediaAttachment> Attachments { get; set; } = new List<MediaAttachment>();
}
