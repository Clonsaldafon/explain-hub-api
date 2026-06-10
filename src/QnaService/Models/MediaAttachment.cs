using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QnaService.Models;

[Table("media_attachments")]
public class MediaAttachment
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("question_id")]
    public Guid? QuestionId { get; set; }

    [Column("answer_id")]
    public Guid? AnswerId { get; set; }

    [Required]
    [Column("object_name")]
    public string ObjectName { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("content_type")]
    public string ContentType { get; set; } = string.Empty;

    [Column("size")]
    public long Size { get; set; }

    [Column("uploaded_by_user_id")]
    public Guid UploadedByUserId { get; set; }

    [Column("is_deleted")]
    public bool IsDeleted { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Question? Question { get; set; }
    public Answer? Answer { get; set; }
}
