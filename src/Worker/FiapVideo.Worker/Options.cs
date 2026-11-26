namespace FiapVideo.Worker;

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

public class ProcessingOptions
{
    public string FfmpegPath { get; init; } = "ffmpeg";
    public double FrameIntervalSeconds { get; init; } = 1.0;
    public int Concurrency { get; init; } = 2;
    public int TimeoutMinutes { get; init; } = 10;
    public int MaxTentativas { get; init; } = 3;
}
