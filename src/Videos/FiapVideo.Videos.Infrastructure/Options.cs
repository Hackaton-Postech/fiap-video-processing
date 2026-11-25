namespace FiapVideo.Videos.Infrastructure;

public class StorageOptions
{
    public string ServiceUrl { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public string Bucket { get; init; } = "videos";
    public string Region { get; init; } = "us-east-1";
}

public class RabbitMqOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Exchange { get; init; } = "video.events";
}

public static class RoutingKeys
{
    public const string VideoEnviado = "video.enviado";
}
