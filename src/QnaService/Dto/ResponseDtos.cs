namespace QnaService.Dto;

public record PagedResultDto<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record MediaAttachmentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string Url,
    DateTime CreatedAt);

public record QuestionSummaryDto(
    Guid Id,
    Guid AuthorId,
    string AuthorEmail,
    string Title,
    string Body,
    int LikeCount,
    int AnswerCount,
    int ViewCount,
    int AttachmentCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record QuestionDetailsDto(
    Guid Id,
    Guid AuthorId,
    string AuthorEmail,
    string Title,
    string Body,
    int LikeCount,
    int AnswerCount,
    int ViewCount,
    IReadOnlyCollection<MediaAttachmentDto> Attachments,
    IReadOnlyCollection<AnswerDto> Answers,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AnswerDto(
    Guid Id,
    Guid QuestionId,
    Guid AuthorId,
    string AuthorEmail,
    string Body,
    int LikeCount,
    IReadOnlyCollection<MediaAttachmentDto> Attachments,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record LikeResultDto(Guid TargetId, int LikeCount, bool Liked);
