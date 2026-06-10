using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthService.Data;
using AuthService.Models;
using AuthService.Services;
using AuthService.Dto;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using AuthService.Events;

namespace AuthService.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly RabbitMqPublisher _rabbitMqPublisher;

    public AuthController(AppDbContext db, JwtService jwt, RabbitMqPublisher rabbitMqPublisher)
    {
        _db = db;
        _jwt = jwt;
        _rabbitMqPublisher = rabbitMqPublisher;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email))
            return BadRequest("Email already exists");
        
        var confirmationToken = Guid.NewGuid().ToString();
        var user = new User
        {
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = UserRole.User,
            IsEmailConfirmed = false,
            EmailConfirmationToken = confirmationToken
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var confirmationLink = $"http://localhost:8080/api/v1/auth/confirm-email?token={confirmationToken}";
        await _rabbitMqPublisher.PublishConfirmationEmailAsync(user.Id, user.Email, confirmationLink);

        return Ok(new {
            UserId = user.Id,
            Email = user.Email,
            NeedsConfirmation = true,
            Message = "Please confirm your email address. Confirmation link has been sent"
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        
        if (user == null)
            return NotFound("User not found");
        
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");
        
        if (!user.IsEmailConfirmed)
            return Unauthorized("Email not confirmed. Please check your inbox");
        
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = _jwt.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_jwt.Config["Jwt:RefreshTokenExpirationDays"]!))
        });
        await _db.SaveChangesAsync();
        
        return Ok(new { AccessToken = accessToken, RefreshToken = refreshToken, UserId = user.Id, Email = user.Email });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshDto dto)
    {
        var principal = _jwt.GetPrincipalFromExpiredToken(dto.AccessToken);

        if (principal == null)
            return Unauthorized("Invalid access token");

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized("Invalid token claims");

        var refreshToken = await _db.RefreshTokens.FirstOrDefaultAsync(rt =>
            rt.Token == dto.RefreshToken && rt.UserId == userId && !rt.IsRevoked
        );

        if (refreshToken == null)
            return Unauthorized("Invalid refresh token");
        
        if (refreshToken.ExpiresAt < DateTime.UtcNow)
            return Unauthorized("Refresh token is expired");

        var user = await _db.Users.FindAsync(userId);
        
        if (user == null)
            return NotFound("User not found");
        
        var newAccessToken = _jwt.GenerateAccessToken(user);
        var newRefreshToken = _jwt.GenerateRefreshToken();

        refreshToken.IsRevoked = true;
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_jwt.Config["Jwt:RefreshTokenExpirationDays"]!))
        });
        await _db.SaveChangesAsync();

        return Ok(new { AccessToken = newAccessToken, RefreshToken = newRefreshToken, UserId = user.Id, Email = user.Email });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("Invalid token");

        var userId = Guid.Parse(userIdClaim);
        var refreshTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId)
            .ToListAsync();

        foreach (var token in refreshTokens)
        {
            token.IsRevoked = true;
        }
        await _db.SaveChangesAsync();

        return Ok(new { message = "All refresh tokens revoked" });
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string token)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.EmailConfirmationToken == token);

        if (user == null)
            return BadRequest("Invalid or expired confirmation token");
        
        user.IsEmailConfirmed = true;
        user.EmailConfirmationToken = null;
        await _db.SaveChangesAsync();

        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = _jwt.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(double.Parse(_jwt.Config["Jwt:RefreshTokenExpirationDays"]!))
        });
        await _db.SaveChangesAsync();

        return Ok(new
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            UserId = user.Id,
            Email = user.Email,
            Message = "Email confirmed successfully. You are now logged in"
        });
    }

    [HttpDelete("users/{userId}")]
    [Authorize]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);

        if (user == null)
            return NotFound("User not found");
        
        var deletedEvent = new UserDeletedEvent
        {
            UserId = userId,
            DeletedAt = DateTime.UtcNow
        };

        await _rabbitMqPublisher.PublishUserDeletedAsync(deletedEvent);

        user.IsDeleted = true;
        await _db.SaveChangesAsync();

        return Accepted(new { message = "User deletion initiated" });
    }
}
