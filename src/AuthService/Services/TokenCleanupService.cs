using Microsoft.EntityFrameworkCore;
using AuthService.Data;

namespace AuthService.Services;

public class TokenCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _expiredRevokedRetention = TimeSpan.FromDays(30);

    public TokenCleanupService(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Cleanup(ct);
            await Task.Delay(_cleanupInterval, ct);
        }
    }

    private async Task Cleanup(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow - _expiredRevokedRetention;
        var tokensToDelete = await db.RefreshTokens
            .Where(rt => rt.IsRevoked && rt.ExpiresAt < cutoff)
            .ToListAsync(ct);
        
        if (tokensToDelete.Any())
        {
            db.RefreshTokens.RemoveRange(tokensToDelete);
            await db.SaveChangesAsync();
        }
    }
}
