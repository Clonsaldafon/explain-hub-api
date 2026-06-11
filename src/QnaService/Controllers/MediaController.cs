using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QnaService.Data;
using QnaService.Dto;
using QnaService.Models;
using QnaService.Services;

namespace QnaService.Controllers;

[ApiController]
[Route("api/v1")]
public class MediaController : ApiControllerBase
{
    private readonly QnaDbContext _db;
    private readonly IObjectStorageService _storage;

    public MediaController(QnaDbContext db, IObjectStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    [HttpPost("questions/{questionId:guid}/attachments")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<MediaAttachmentDto>> AttachToQuestion(
        Guid questionId,
        IFormFile file,
        CancellationToken ct)
    {
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == questionId && !q.IsDeleted, ct);

        if (question == null)
            return NotFound("Question not found");

        if (!IsOwnerOrAdmin(question.AuthorId))
            return Forbid();

        if (!_storage.TryValidate(file, out var validationError))
            return BadRequest(validationError);

        var currentUser = GetCurrentUser();
        var stored = await _storage.UploadAsync(file, $"questions/{question.Id}", ct);
        var attachment = new MediaAttachment
        {
            QuestionId = question.Id,
            ObjectName = stored.ObjectName,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            Size = stored.Size,
            UploadedByUserId = currentUser.Id
        };

        _db.MediaAttachments.Add(attachment);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            await _storage.DeleteAsync(stored.ObjectName, ct);
            throw;
        }

        return Created($"/api/v1/media/{attachment.Id}", ToDto(attachment, stored.Url));
    }

    [HttpPost("answers/{answerId:guid}/attachments")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100_000_000)]
    public async Task<ActionResult<MediaAttachmentDto>> AttachToAnswer(
        Guid answerId,
        IFormFile file,
        CancellationToken ct)
    {
        var answer = await _db.Answers
            .Include(a => a.Question)
            .FirstOrDefaultAsync(a => a.Id == answerId && !a.IsDeleted && !a.Question.IsDeleted, ct);

        if (answer == null)
            return NotFound("Answer not found");

        if (!IsOwnerOrAdmin(answer.AuthorId))
            return Forbid();

        if (!_storage.TryValidate(file, out var validationError))
            return BadRequest(validationError);

        var currentUser = GetCurrentUser();
        var stored = await _storage.UploadAsync(file, $"answers/{answer.Id}", ct);
        var attachment = new MediaAttachment
        {
            AnswerId = answer.Id,
            ObjectName = stored.ObjectName,
            FileName = stored.FileName,
            ContentType = stored.ContentType,
            Size = stored.Size,
            UploadedByUserId = currentUser.Id
        };

        _db.MediaAttachments.Add(attachment);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            await _storage.DeleteAsync(stored.ObjectName, ct);
            throw;
        }

        return Created($"/api/v1/media/{attachment.Id}", ToDto(attachment, stored.Url));
    }

    [HttpGet("media/{id:guid}")]
    public async Task<ActionResult<MediaAttachmentDto>> GetMedia(Guid id, CancellationToken ct)
    {
        var attachment = await _db.MediaAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        if (attachment == null)
            return NotFound("Media attachment not found");

        var url = await _storage.GetReadUrlAsync(attachment.ObjectName, ct);
        return Ok(ToDto(attachment, url));
    }

    [HttpDelete("media/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteMedia(Guid id, CancellationToken ct)
    {
        var attachment = await _db.MediaAttachments
            .Include(a => a.Question)
            .Include(a => a.Answer)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);

        if (attachment == null)
            return NotFound("Media attachment not found");

        var ownerId = attachment.Question?.AuthorId ?? attachment.Answer?.AuthorId;
        if (ownerId == null || (!IsOwnerOrAdmin(ownerId.Value) && TryGetCurrentUserId() != attachment.UploadedByUserId))
            return Forbid();

        await _storage.DeleteAsync(attachment.ObjectName, ct);
        attachment.IsDeleted = true;
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static MediaAttachmentDto ToDto(MediaAttachment attachment, string url)
    {
        return new MediaAttachmentDto(
            attachment.Id,
            attachment.FileName,
            attachment.ContentType,
            attachment.Size,
            url,
            attachment.CreatedAt);
    }
}
