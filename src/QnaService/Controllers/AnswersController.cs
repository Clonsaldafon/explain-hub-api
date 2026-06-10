using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QnaService.Data;
using QnaService.Dto;
using QnaService.Models;
using QnaService.Services;

namespace QnaService.Controllers;

[ApiController]
[Route("api/v1/answers")]
public class AnswersController : ApiControllerBase
{
    private readonly QnaDbContext _db;
    private readonly IObjectStorageService _storage;
    private readonly RabbitMqLikePublisher _likePublisher;
    private readonly IConfiguration _configuration;

    public AnswersController(
        QnaDbContext db,
        IObjectStorageService storage,
        RabbitMqLikePublisher likePublisher,
        IConfiguration configuration)
    {
        _db = db;
        _storage = storage;
        _likePublisher = likePublisher;
        _configuration = configuration;
    }

    [HttpGet("{id:guid}", Name = "GetAnswerById")]
    public async Task<ActionResult<AnswerDto>> GetAnswer(Guid id, CancellationToken ct)
    {
        var answer = await _db.Answers
            .AsNoTracking()
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        if (answer == null)
            return NotFound("Answer not found");

        return Ok(await MapAnswerAsync(answer, ct));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<AnswerDto>> UpdateAnswer(Guid id, UpdateAnswerDto dto, CancellationToken ct)
    {
        var answer = await _db.Answers
            .Include(a => a.Attachments)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        if (answer == null)
            return NotFound("Answer not found");

        if (!IsOwnerOrAdmin(answer.AuthorId))
            return Forbid();

        answer.Body = dto.Body.Trim();
        answer.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(await MapAnswerAsync(answer, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteAnswer(Guid id, CancellationToken ct)
    {
        var answer = await _db.Answers
            .Include(a => a.Question)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        if (answer == null)
            return NotFound("Answer not found");

        if (!IsOwnerOrAdmin(answer.AuthorId))
            return Forbid();

        answer.IsDeleted = true;
        answer.UpdatedAt = DateTime.UtcNow;
        answer.Question.AnswerCount = Math.Max(0, answer.Question.AnswerCount - 1);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("{id:guid}/like")]
    [Authorize]
    public async Task<ActionResult<LikeResultDto>> LikeAnswer(Guid id, CancellationToken ct)
    {
        var answer = await _db.Answers
            .Include(a => a.Question)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted && !a.Question.IsDeleted, ct);

        if (answer == null)
            return NotFound("Answer not found");

        var currentUser = GetCurrentUser();
        var alreadyLiked = await _db.AnswerLikes.AnyAsync(
            l => l.AnswerId == id && l.UserId == currentUser.Id,
            ct);

        if (alreadyLiked)
            return Ok(new LikeResultDto(answer.Id, answer.LikeCount, true));

        _db.AnswerLikes.Add(new AnswerLike
        {
            AnswerId = answer.Id,
            UserId = currentUser.Id,
            UserEmail = currentUser.Email
        });
        answer.LikeCount++;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            await _db.Entry(answer).ReloadAsync(ct);
            return Ok(new LikeResultDto(answer.Id, answer.LikeCount, true));
        }

        await PublishLikeNotificationAsync(
            answer.AuthorId,
            answer.AuthorEmail,
            currentUser,
            answer.Question.Title,
            BuildAnswerUrl(answer.QuestionId, answer.Id),
            "answer",
            ct);

        return Ok(new LikeResultDto(answer.Id, answer.LikeCount, true));
    }

    [HttpDelete("{id:guid}/like")]
    [Authorize]
    public async Task<ActionResult<LikeResultDto>> UnlikeAnswer(Guid id, CancellationToken ct)
    {
        var currentUser = GetCurrentUser();
        var answer = await _db.Answers.FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        if (answer == null)
            return NotFound("Answer not found");

        var like = await _db.AnswerLikes.FirstOrDefaultAsync(
            l => l.AnswerId == id && l.UserId == currentUser.Id,
            ct);

        if (like == null)
            return Ok(new LikeResultDto(answer.Id, answer.LikeCount, false));

        _db.AnswerLikes.Remove(like);
        answer.LikeCount = Math.Max(0, answer.LikeCount - 1);
        await _db.SaveChangesAsync(ct);

        return Ok(new LikeResultDto(answer.Id, answer.LikeCount, false));
    }

    private async Task<AnswerDto> MapAnswerAsync(Answer answer, CancellationToken ct)
    {
        var attachments = new List<MediaAttachmentDto>();

        foreach (var attachment in answer.Attachments.Where(a => !a.IsDeleted).OrderBy(a => a.CreatedAt))
        {
            attachments.Add(new MediaAttachmentDto(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.Size,
                await _storage.GetReadUrlAsync(attachment.ObjectName, ct),
                attachment.CreatedAt));
        }

        return new AnswerDto(
            answer.Id,
            answer.QuestionId,
            answer.AuthorId,
            answer.AuthorEmail,
            answer.Body,
            answer.LikeCount,
            attachments,
            answer.CreatedAt,
            answer.UpdatedAt);
    }

    private async Task PublishLikeNotificationAsync(
        Guid recipientUserId,
        string recipientEmail,
        CurrentUser liker,
        string questionTitle,
        string url,
        string targetType,
        CancellationToken ct)
    {
        if (recipientUserId == liker.Id)
            return;

        await _likePublisher.PublishAsync(new LikeNotificationMessage
        {
            Recipient = recipientEmail,
            LikerName = liker.Email,
            PostTitle = questionTitle,
            Url = url,
            TargetType = targetType
        }, ct);
    }

    private string BuildAnswerUrl(Guid questionId, Guid answerId)
    {
        var publicBaseUrl = _configuration["App:PublicBaseUrl"]?.TrimEnd('/');
        var path = $"/api/v1/questions/{questionId}#answer-{answerId}";
        return string.IsNullOrWhiteSpace(publicBaseUrl) ? path : $"{publicBaseUrl}{path}";
    }
}
