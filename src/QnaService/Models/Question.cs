using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QnaService.Models;

[Table("questions")]
public class Question
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("author_id")]
    public Guid AuthorId { get; set; }

    [Required]
    [EmailAddress]
    [Column("author_email")]
    public string AuthorEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Column("like_count")]
    public int LikeCount { get; set; }

    [Column("answer_count")]
    public int AnswerCount { get; set; }

    [Column("view_count")]
    public int ViewCount { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    public ICollection<QuestionLike> Likes { get; set; } = new List<QuestionLike>();
    public ICollection<QuestionView> Views { get; set; } = new List<QuestionView>();
    public ICollection<MediaAttachment> Attachments { get; set; } = new List<MediaAttachment>();
}
