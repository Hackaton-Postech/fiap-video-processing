namespace FiapVideo.Videos.Infrastructure;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CriadoEm { get; private set; }
    public DateTime? PublicadoEm { get; set; }
    public int Tentativas { get; set; }
    public string? UltimoErro { get; set; }

    private OutboxMessage() { }

    public static OutboxMessage Criar(string tipo, string payload) => new()
    {
        Id = Guid.NewGuid(),
        Tipo = tipo,
        Payload = payload,
        CriadoEm = DateTime.UtcNow
    };
}
