using System.ComponentModel.DataAnnotations;

namespace QnaService.Dto;

public class CreateQuestionDto
{
    [Required]
    [MinLength(5)]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MinLength(10)]
    [MaxLength(10000)]
    public string Body { get; set; } = string.Empty;
}
