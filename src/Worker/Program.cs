using Worker.Consumers;
using Worker.Infrastructure.Email;
using Worker.Infrastructure.Messaging;
using Worker.Messages;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.Configure<RabbitMqConfiguration>(builder.Configuration.GetSection("RabbitMQ"));

builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddTransient<IConsumer<ConfirmEmailMessage>, ConfirmEmailConsumer>();
builder.Services.AddTransient<IConsumer<LikeNotificationMessage>, LikeNotificationConsumer>();

builder.Services.AddHostedService<RabbitMqListener>();

var host = builder.Build();
host.Run();