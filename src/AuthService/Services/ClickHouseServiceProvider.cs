using System.Net.Http.Json;

namespace AuthService.Services;

public class ClickHouseLoggerProvider(string analyticsUrl, string serviceName) : ILoggerProvider
{
    private readonly HttpClient _httpClient = new() { BaseAddress = new Uri(analyticsUrl) };

    public ILogger CreateLogger(string categoryName) => new ClickHouseLogger(_httpClient, serviceName, categoryName);
    public void Dispose() => _httpClient.Dispose();
}

public class ClickHouseLogger : ILogger
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceName;
    private readonly string _categoryName;

    public ClickHouseLogger(HttpClient httpClient, string serviceName, string categoryName)
    {
        _httpClient = httpClient;
        _serviceName = serviceName;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var payload = new
        {
            ServiceName = _serviceName,
            Level = logLevel.ToString(),
            Message = $"[{_categoryName}] {message}"
        };
        
        _ = _httpClient.PostAsJsonAsync("api/analytics/logs", payload);
    }
}