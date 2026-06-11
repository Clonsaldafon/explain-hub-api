using MassTransit;
using Microsoft.EntityFrameworkCore;
using QnaService.Data;
using QnaService.Events;

namespace QnaService.Services;

public class UserDeletedConsumer(
    IServiceProvider services,
    ISendEndpointProvider sendEndpointProvider,
    ILogger<UserDeletedConsumer> logger) : IConsumer<UserDeletedEvent>
{
    public async Task Consume(ConsumeContext<UserDeletedEvent> context)
    {
        var (success, error) = await ProcessUserDeletionAsync(context.Message.UserId, context.CancellationToken);
        await SendResponseAsync(context.Message.UserId, success, error, context.CancellationToken);
    }

    private async Task<(bool Success, string? Error)> ProcessUserDeletionAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<QnaDbContext>();

            await db.Questions
                .Where(q => q.AuthorId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true), ct);

            await db.Answers
                .Where(a => a.AuthorId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true), ct);

            return (true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete Q&A content for user {UserId}", userId);
            return (false, ex.Message);
        }
    }

    private async Task SendResponseAsync(Guid userId, bool success, string? error, CancellationToken ct)
    {
        var responseEvent = new UserContentDeletedEvent
        {
            UserId = userId,
            Success = success,
            Error = error
        };

        var endpoint = await sendEndpointProvider.GetSendEndpoint(new Uri("queue:user_content_deleted"));
        await endpoint.Send(responseEvent, ct);
    }
}
