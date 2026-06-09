using Microsoft.EntityFrameworkCore;
using AuthService.Data;
using Consul;
using AuthService.Models;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using AuthService.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuthDb")));

var jwtKey = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddScoped<JwtService>();

builder.Services.AddHostedService<TokenCleanupService>();

builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

builder.Services.AddSingleton(sp =>
{
    var publisher = new RabbitMqPublisher(sp.GetRequiredService<IConfiguration>());
    return publisher;
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    Console.WriteLine($"Request: {context.Request.Method} {context.Request.Path} Protocol: {context.Request.Protocol}");
    await next();
});

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();

    var adminEmail = builder.Configuration["Admin:Email"] ?? "admin@example.com";
    var adminPassword = builder.Configuration["Admin:Password"] ?? "admin123";

    if (!dbContext.Users.Any(u => u.Email == adminEmail))
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword);
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = adminEmail,
            PasswordHash = passwordHash,
            Role = UserRole.Admin,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow
        });
        dbContext.SaveChanges();
        Console.WriteLine($"Admin user created: {adminEmail}");
    }
}

var consulClient = new ConsulClient(config =>
{
    config.Address = new Uri(builder.Configuration["Consul:Host"] ?? "http://consul:8500");
});

var registration = new AgentServiceRegistration
{
    ID = $"auth-service-{Guid.NewGuid()}",
    Name = "auth-service",
    Address = "auth-service",
    Port = 80,
    Check = new AgentServiceCheck
    {
        HTTP = "http://auth-service/health",
        Interval = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(5),
        DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
    }
};

await consulClient.Agent.ServiceRegister(registration);

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userIdClaim != null && Guid.TryParse(userIdClaim, out var userId))
        {
            using var scope = context.RequestServices.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || !user.IsEmailConfirmed)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsync("Email not confirmed");

                return;
            }
        }
    }

    await next();
});

app.MapControllers();

app.MapGet("/check-db", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    return canConnect ? "Database connection OK" : "Database connection FAILED";
});

app.MapGet("/health", () => Results.Ok("Healthy"));

app.MapGrpcService<UserService>().RequireHost("*:5001");
app.MapGrpcReflectionService();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

var publisher = app.Services.GetRequiredService<RabbitMqPublisher>();
AppDomain.CurrentDomain.ProcessExit += async (s, e) => await publisher.DisposeAsync();

app.Run();
