using System.ComponentModel.DataAnnotations;

namespace QnaService.Dto;

public class CreateAnswerDto
{
    [Required]
    [MinLength(2)]
    [MaxLength(10000)]
    public string Body { get; set; } = string.Empty;
}
