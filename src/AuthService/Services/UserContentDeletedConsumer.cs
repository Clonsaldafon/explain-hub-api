using AuthService.Data;
using AuthService.Events;
using MassTransit;

namespace AuthService.Services;

public class UserContentDeletedConsumer(
    IServiceProvider services,
    ILogger<UserContentDeletedConsumer> logger) : IConsumer<UserContentDeletedEvent>
{
    public async Task Consume(ConsumeContext<UserContentDeletedEvent> context)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var message = context.Message;
            var user = await db.Users.FindAsync(new object[] { message.UserId }, cancellationToken: context.CancellationToken);

            if (user == null)
                return;

            if (message.Success)
            {
                db.Users.Remove(user);
                await db.SaveChangesAsync(context.CancellationToken);
                logger.LogInformation("Saga success: User {UserId} permanently deleted after content cleanup.", message.UserId);
            }
            else
            {
                user.IsDeleted = false;
                await db.SaveChangesAsync(context.CancellationToken);
                logger.LogInformation("Saga aborted: User {UserId} restored. Cleanup error: {Error}", message.UserId, message.Error);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing user content deleted event");
        }
    }
}
