using FiapVideo.Notification;

var builder = Host.CreateApplicationBuilder(args);

var rabbit = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
var smtp = builder.Configuration.GetSection("Smtp").Get<SmtpOptions>() ?? new SmtpOptions();

if (string.IsNullOrWhiteSpace(rabbit.User) || string.IsNullOrWhiteSpace(rabbit.Password))
    throw new InvalidOperationException("RabbitMq:User e RabbitMq:Password são obrigatórios.");
if (string.IsNullOrWhiteSpace(smtp.Host) || string.IsNullOrWhiteSpace(smtp.From))
    throw new InvalidOperationException("Smtp:Host e Smtp:From são obrigatórios.");

builder.Services.AddSingleton(rabbit);
builder.Services.AddSingleton(smtp);
builder.Services.AddSingleton<INotificador, SmtpNotificador>();
builder.Services.AddHostedService<NotificationConsumer>();

await builder.Build().RunAsync();
