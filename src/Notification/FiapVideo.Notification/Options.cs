namespace FiapVideo.Notification;

public class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Exchange { get; init; } = "video.events";
}

public class SmtpOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public string From { get; init; } = "nao-responda@fiapx.local";
    public string FromName { get; init; } = "FIAP X Vídeos";
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool UseTls { get; init; }
}
