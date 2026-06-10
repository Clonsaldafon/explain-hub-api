using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QnaService.Data;
using QnaService.Dto;
using QnaService.Models;
using QnaService.Services;

namespace QnaService.Controllers;

[ApiController]
[Route("api/v1/questions")]
public class QuestionsController : ApiControllerBase
{
    private readonly QnaDbContext _db;
    private readonly IObjectStorageService _storage;
    private readonly RabbitMqLikePublisher _likePublisher;
    private readonly IConfiguration _configuration;

    public QuestionsController(
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

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<QuestionSummaryDto>>> GetQuestions(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Questions
            .AsNoTracking()
            .Where(q => !q.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(q =>
                EF.Functions.ILike(q.Title, pattern) ||
                EF.Functions.ILike(q.Body, pattern));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(q => new QuestionSummaryDto(
                q.Id,
                q.AuthorId,
                q.AuthorEmail,
                q.Title,
                q.Body,
                q.LikeCount,
                q.AnswerCount,
                q.ViewCount,
                q.Attachments.Count(a => !a.IsDeleted),
                q.CreatedAt,
                q.UpdatedAt))
            .ToListAsync(ct);

        return Ok(new PagedResultDto<QuestionSummaryDto>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:guid}", Name = "GetQuestionById")]
    public async Task<ActionResult<QuestionDetailsDto>> GetQuestion(Guid id, CancellationToken ct)
    {
        var question = await _db.Questions
            .Include(q => q.Attachments)
            .Include(q => q.Answers)
                .ThenInclude(a => a.Attachments)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct);

        if (question == null)
            return NotFound("Question not found");

        question.ViewCount++;
        _db.QuestionViews.Add(new QuestionView
        {
            QuestionId = question.Id,
            ViewerId = TryGetCurrentUserId(),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers["User-Agent"].ToString()
        });

        await _db.SaveChangesAsync(ct);

        return Ok(await MapQuestionDetailsAsync(question, ct));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<QuestionDetailsDto>> CreateQuestion(CreateQuestionDto dto, CancellationToken ct)
    {
        var currentUser = GetCurrentUser();
        var question = new Question
        {
            AuthorId = currentUser.Id,
            AuthorEmail = currentUser.Email,
            Title = dto.Title.Trim(),
            Body = dto.Body.Trim()
        };

        _db.Questions.Add(question);
        await _db.SaveChangesAsync(ct);

        var response = await MapQuestionDetailsAsync(question, ct);
        return CreatedAtAction(nameof(GetQuestion), new { id = question.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<QuestionDetailsDto>> UpdateQuestion(Guid id, UpdateQuestionDto dto, CancellationToken ct)
    {
        var question = await _db.Questions
            .Include(q => q.Attachments)
            .Include(q => q.Answers)
                .ThenInclude(a => a.Attachments)
            .FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct);

        if (question == null)
            return NotFound("Question not found");

        if (!IsOwnerOrAdmin(question.AuthorId))
            return Forbid();

        if (!string.IsNullOrWhiteSpace(dto.Title))
            question.Title = dto.Title.Trim();

        if (!string.IsNullOrWhiteSpace(dto.Body))
            question.Body = dto.Body.Trim();

        question.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(await MapQuestionDetailsAsync(question, ct));
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteQuestion(Guid id, CancellationToken ct)
    {
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct);

        if (question == null)
            return NotFound("Question not found");

        if (!IsOwnerOrAdmin(question.AuthorId))
            return Forbid();

        question.IsDeleted = true;
        question.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("{id:guid}/answers")]
    [Authorize]
    public async Task<ActionResult<AnswerDto>> CreateAnswer(Guid id, CreateAnswerDto dto, CancellationToken ct)
    {
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct);

        if (question == null)
            return NotFound("Question not found");

        var currentUser = GetCurrentUser();
        var answer = new Answer
        {
            QuestionId = question.Id,
            AuthorId = currentUser.Id,
            AuthorEmail = currentUser.Email,
            Body = dto.Body.Trim()
        };

        _db.Answers.Add(answer);
        question.AnswerCount++;
        await _db.SaveChangesAsync(ct);

        return Created($"/api/v1/answers/{answer.Id}", await MapAnswerAsync(answer, ct));
    }

    [HttpPost("{id:guid}/like")]
    [Authorize]
    public async Task<ActionResult<LikeResultDto>> LikeQuestion(Guid id, CancellationToken ct)
    {
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct);

        if (question == null)
            return NotFound("Question not found");

        var currentUser = GetCurrentUser();
        var alreadyLiked = await _db.QuestionLikes.AnyAsync(
            l => l.QuestionId == id && l.UserId == currentUser.Id,
            ct);

        if (alreadyLiked)
            return Ok(new LikeResultDto(question.Id, question.LikeCount, true));

        _db.QuestionLikes.Add(new QuestionLike
        {
            QuestionId = question.Id,
            UserId = currentUser.Id,
            UserEmail = currentUser.Email
        });
        question.LikeCount++;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            await _db.Entry(question).ReloadAsync(ct);
            return Ok(new LikeResultDto(question.Id, question.LikeCount, true));
        }

        await PublishLikeNotificationAsync(
            question.AuthorId,
            question.AuthorEmail,
            currentUser,
            question.Title,
            BuildQuestionUrl(question.Id),
            "question",
            ct);

        return Ok(new LikeResultDto(question.Id, question.LikeCount, true));
    }

    [HttpDelete("{id:guid}/like")]
    [Authorize]
    public async Task<ActionResult<LikeResultDto>> UnlikeQuestion(Guid id, CancellationToken ct)
    {
        var currentUser = GetCurrentUser();
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id && !q.IsDeleted, ct);

        if (question == null)
            return NotFound("Question not found");

        var like = await _db.QuestionLikes.FirstOrDefaultAsync(
            l => l.QuestionId == id && l.UserId == currentUser.Id,
            ct);

        if (like == null)
            return Ok(new LikeResultDto(question.Id, question.LikeCount, false));

        _db.QuestionLikes.Remove(like);
        question.LikeCount = Math.Max(0, question.LikeCount - 1);
        await _db.SaveChangesAsync(ct);

        return Ok(new LikeResultDto(question.Id, question.LikeCount, false));
    }

    private async Task<QuestionDetailsDto> MapQuestionDetailsAsync(Question question, CancellationToken ct)
    {
        var attachments = await MapAttachmentsAsync(
            question.Attachments.Where(a => !a.IsDeleted).OrderBy(a => a.CreatedAt),
            ct);

        var answers = new List<AnswerDto>();
        foreach (var answer in question.Answers.Where(a => !a.IsDeleted).OrderBy(a => a.CreatedAt))
        {
            answers.Add(await MapAnswerAsync(answer, ct));
        }

        return new QuestionDetailsDto(
            question.Id,
            question.AuthorId,
            question.AuthorEmail,
            question.Title,
            question.Body,
            question.LikeCount,
            question.AnswerCount,
            question.ViewCount,
            attachments,
            answers,
            question.CreatedAt,
            question.UpdatedAt);
    }

    private async Task<AnswerDto> MapAnswerAsync(Answer answer, CancellationToken ct)
    {
        var attachments = await MapAttachmentsAsync(
            answer.Attachments.Where(a => !a.IsDeleted).OrderBy(a => a.CreatedAt),
            ct);

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

    private async Task<IReadOnlyCollection<MediaAttachmentDto>> MapAttachmentsAsync(
        IEnumerable<MediaAttachment> attachments,
        CancellationToken ct)
    {
        var result = new List<MediaAttachmentDto>();

        foreach (var attachment in attachments)
        {
            result.Add(new MediaAttachmentDto(
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.Size,
                await _storage.GetReadUrlAsync(attachment.ObjectName, ct),
                attachment.CreatedAt));
        }

        return result;
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

    private string BuildQuestionUrl(Guid questionId)
    {
        var publicBaseUrl = _configuration["App:PublicBaseUrl"]?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(publicBaseUrl)
            ? $"/api/v1/questions/{questionId}"
            : $"{publicBaseUrl}/api/v1/questions/{questionId}";
    }
}
