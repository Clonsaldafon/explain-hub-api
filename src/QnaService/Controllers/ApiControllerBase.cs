using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace QnaService.Controllers;

public abstract class ApiControllerBase : ControllerBase
{
    protected CurrentUser GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid");

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Authenticated user email claim is missing");

        return new CurrentUser(userId, email);
    }

    protected Guid? TryGetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    protected bool IsOwnerOrAdmin(Guid ownerId)
    {
        return TryGetCurrentUserId() == ownerId || User.IsInRole("Admin");
    }
}

public record CurrentUser(Guid Id, string Email);
