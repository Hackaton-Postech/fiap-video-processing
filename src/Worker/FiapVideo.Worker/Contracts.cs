using System.Text.Json;

namespace FiapVideo.Worker;

public record VideoEnviado(
    Guid VideoId, Guid UsuarioId, string Email, string Nome,
    string ChaveOriginal, string NomeOriginal, DateTime EnviadoEmUtc);

public record VideoProcessando(Guid VideoId, DateTime EmUtc);

public record VideoProcessado(
    Guid VideoId, Guid UsuarioId, string Email, string Nome, string NomeOriginal,
    string ChaveZip, int TotalFrames, DateTime EmUtc);

public record VideoFalhou(
    Guid VideoId, Guid UsuarioId, string Email, string Nome, string NomeOriginal,
    string Mensagem, DateTime EmUtc);

public static class RoutingKeys
{
    public const string VideoEnviado = "video.enviado";
    public const string VideoProcessando = "video.processando";
    public const string VideoProcessado = "video.processado";
    public const string VideoFalhou = "video.falhou";
}

public static class JsonConfig
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
