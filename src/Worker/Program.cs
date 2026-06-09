using MassTransit;
using Worker.Consumers;
using Worker.Infrastructure.Email;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ConfirmEmailConsumer>();
    x.AddConsumer<LikeNotificationConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var host = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";

        cfg.Host(host, "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:UserName"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ClearSerialization();
        cfg.UseRawJsonSerializer();

        cfg.ReceiveEndpoint("confirm-email-queue", e =>
        {
            e.Bind("email-exchange", b =>
            {
                b.ExchangeType = "direct";
                b.RoutingKey = "confirm-email";
            });
            e.ConfigureConsumer<ConfirmEmailConsumer>(context);
        });

        cfg.ReceiveEndpoint("like-notification-queue", e =>
        {
            e.Bind("email-exchange", b =>
            {
                b.ExchangeType = "direct";
                b.RoutingKey = "like-notification";
            });
            e.ConfigureConsumer<LikeNotificationConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();