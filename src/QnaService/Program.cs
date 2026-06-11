using System.Text;
using Consul;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using QnaService.Data;
using QnaService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserDeletedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";

        cfg.Host(host, "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:UserName"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ClearSerialization();
        cfg.UseRawJsonSerializer(RawSerializerOptions.AnyMessageType);

        cfg.ReceiveEndpoint(builder.Configuration["RabbitMQ:QueueNameUserDeleted"] ?? "user.deleted", e =>
        {
            e.ConfigureConsumer<UserDeletedConsumer>(context);
        });
    });
});

builder.Services.AddDbContext<QnaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("QnaDb")));

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Jwt:Key is missing");

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio"));
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));
builder.Services.AddSingleton<IObjectStorageService, MinioObjectStorageService>();
builder.Services.AddScoped<RabbitMqLikePublisher>();

var analyticsUrl = builder.Configuration["Analytics:ServiceUrl"] ?? "http://analytics-service:8080/";
builder.Logging.AddProvider(new ClickHouseLoggerProvider(analyticsUrl, "qna-service"));

var app = builder.Build();

app.UseHttpMetrics();
    
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<QnaDbContext>();
    await dbContext.Database.MigrateAsync();

    var storage = scope.ServiceProvider.GetRequiredService<IObjectStorageService>();
    await storage.EnsureBucketAsync(CancellationToken.None);
}

app.Lifetime.ApplicationStarted.Register(async () =>
{
    await RegisterInConsulAsync(app, builder.Configuration);
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapMetrics();
app.MapHealthChecks("/health");
app.MapGet("/", () => "ExplainHub Q&A service");

app.Run();

static async Task RegisterInConsulAsync(WebApplication app, IConfiguration configuration)
{
    var consulHost = configuration["Consul:Host"];
    if (string.IsNullOrWhiteSpace(consulHost))
        return;

    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ConsulRegistration");
    var consulClient = new ConsulClient(config => config.Address = new Uri(consulHost));
    var serviceAddress = configuration["Consul:ServiceAddress"] ?? "qna-service";
    var servicePort = int.TryParse(configuration["Consul:ServicePort"], out var port) ? port : 80;
    var serviceId = $"qna-service-{Guid.NewGuid()}";

    var registration = new AgentServiceRegistration
    {
        ID = serviceId,
        Name = "qna-service",
        Address = serviceAddress,
        Port = servicePort,
        Check = new AgentServiceCheck
        {
            HTTP = $"http://{serviceAddress}:{servicePort}/health",
            Interval = TimeSpan.FromSeconds(10),
            Timeout = TimeSpan.FromSeconds(5),
            DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
        }
    };

    try
    {
        await consulClient.Agent.ServiceRegister(registration);
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            try
            {
                consulClient.Agent.ServiceDeregister(serviceId).GetAwaiter().GetResult();
            }
            finally
            {
                consulClient.Dispose();
            }
        });
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Q&A service was not registered in Consul");
    }
}
