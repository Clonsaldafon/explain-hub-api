using Microsoft.AspNetCore.Mvc;
using MassTransit;
using ClickHouse.Client.ADO;
using ClickHouse.Client.ADO.Parameters;
using ClickHouse.Client.Utility;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var builderCH = new ClickHouseConnectionStringBuilder
{
    Host = "clickhouse-server",
    Port = 8123,
    Database = "logs_db",
    Username = "analytics",
    Password = "analytics123",
    Protocol = "http"
};

string chConnectionString = builderCH.ConnectionString;

Console.WriteLine(chConnectionString);

builder.Services.AddTransient<ClickHouseConnection>(
    _ => new ClickHouseConnection(chConnectionString));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<LikeEventConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddControllers();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();

try
{
    Console.WriteLine("Opening ClickHouse connection...");

    using var connection = new ClickHouseConnection(chConnectionString);

    await connection.OpenAsync();

    Console.WriteLine("ClickHouse connected.");
    
    
    using var testCommand = connection.CreateCommand();

    testCommand.CommandText = "SELECT currentUser()";

    var currentUser = await testCommand.ExecuteScalarAsync();

    Console.WriteLine($"Connected as: {currentUser}");

    using var command1 = connection.CreateCommand();

    command1.CommandText = @"
        CREATE TABLE IF NOT EXISTS logs_table (
            Timestamp DateTime,
            ServiceName String,
            Level String,
            Message String
        ) ENGINE = MergeTree()
        ORDER BY Timestamp";

    Console.WriteLine("Creating logs_table...");

    await command1.ExecuteNonQueryAsync();

    Console.WriteLine("logs_table created.");

    using var command2 = connection.CreateCommand();

    command2.CommandText = @"
        CREATE TABLE IF NOT EXISTS likes_analytics (
            Timestamp DateTime,
            QuestionId Int32,
            AuthorId Int32
        ) ENGINE = MergeTree()
        ORDER BY Timestamp";

    Console.WriteLine("Creating likes_analytics...");

    await command2.ExecuteNonQueryAsync();

    Console.WriteLine("likes_analytics created.");
}
catch (Exception ex)
{
    Console.WriteLine("CLICKHOUSE ERROR");
    Console.WriteLine(ex.ToString());
}

app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly ClickHouseConnection _connection;
    public AnalyticsController(ClickHouseConnection connection) => _connection = connection;

    [HttpPost("logs")]
    public async Task<IActionResult> ReceiveLog([FromBody] MicroserviceLogDto log)
    {
        await _connection.OpenAsync();
        using var command = _connection.CreateCommand();
        command.CommandText = "INSERT INTO logs_table (Timestamp, ServiceName, Level, Message) VALUES (now(), @service, @level, @message)";
        command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "service", Value = log.ServiceName });
        command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "level", Value = log.Level });
        command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "message", Value = log.Message });
        
        await command.ExecuteNonQueryAsync();
        return Ok(new { status = "Log saved" });
    }
}

public class LikeEventConsumer : IConsumer<LikeTargetEvent>
{
    private readonly ClickHouseConnection _connection;
    public LikeEventConsumer(ClickHouseConnection connection) => _connection = connection;

    public async Task Consume(ConsumeContext<LikeTargetEvent> context)
    {
        var message = context.Message;
        await _connection.OpenAsync();
        using var command = _connection.CreateCommand();
        command.CommandText = "INSERT INTO likes_analytics (Timestamp, QuestionId, AuthorId) VALUES (now(), @qId, @aId)";
        command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "qId", Value = message.QuestionId });
        command.Parameters.Add(new ClickHouseDbParameter { ParameterName = "aId", Value = message.AuthorId });
        
        await command.ExecuteNonQueryAsync();
    }
}

public class MicroserviceLogDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class LikeTargetEvent
{
    public int QuestionId { get; set; }
    public int AuthorId { get; set; }
}
