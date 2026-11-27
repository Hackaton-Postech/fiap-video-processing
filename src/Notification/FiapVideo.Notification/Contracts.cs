using System.Text.Json;

namespace FiapVideo.Notification;

public record VideoFalhou(
    Guid VideoId, Guid UsuarioId, string Email, string Nome, string NomeOriginal,
    string Mensagem, DateTime EmUtc);

public static class RoutingKeys
{
    public const string VideoFalhou = "video.falhou";
}

public static class JsonConfig
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
