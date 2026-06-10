using Consul;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseHttpMetrics();
app.MapReverseProxy();
app.MapHealthChecks("/health");
app.MapMetrics();
app.MapGet("/", () => "ExplainHub API Gateway");

var consulHost = builder.Configuration["Consul:Host"] ?? "http://consul:8500";
var consulClient = new ConsulClient(c => c.Address = new Uri(consulHost));

var registration = new AgentServiceRegistration
{
    ID = $"api-gateway-{Guid.NewGuid()}",
    Name = "api-gateway",
    Address = "api-gateway",
    Port = 80,
    Check = new AgentServiceCheck
    {
        HTTP = "http://api-gateway/health",
        Interval = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(5),
        DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1)
    }
};

await consulClient.Agent.ServiceRegister(registration);

app.Lifetime.ApplicationStopping.Register(() =>
{
    consulClient.Agent.ServiceDeregister(registration.ID).GetAwaiter().GetResult();
    consulClient.Dispose();
});

app.Run();