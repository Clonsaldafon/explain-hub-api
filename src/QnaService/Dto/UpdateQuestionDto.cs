using System.ComponentModel.DataAnnotations;

namespace QnaService.Dto;

public class UpdateQuestionDto
{
    [MinLength(5)]
    [MaxLength(200)]
    public string? Title { get; set; }

    [MinLength(10)]
    [MaxLength(10000)]
    public string? Body { get; set; }
}
