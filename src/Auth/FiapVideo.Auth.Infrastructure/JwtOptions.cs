namespace FiapVideo.Auth.Infrastructure;

public class JwtOptions
{
    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = "fiap-video-auth";
    public string Audience { get; init; } = "fiap-video";
    public int ExpiraEmMinutos { get; init; } = 60;
}
