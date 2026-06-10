using Grpc.Core;
using AuthService.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Services;

public class UserService: AuthGrpcService.AuthGrpcServiceBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserService> _logger;

    public UserService(AppDbContext db, ILogger<UserService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<UserEmailResponse> GetUserEmail(UserRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user ID format"));
        
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"User {request.UserId} not found"));
        
        _logger.LogInformation($"gRPC GetUserEmail called for user {userId}, returning {user.Email}");

        return new UserEmailResponse { Email = user.Email };
    }
}
