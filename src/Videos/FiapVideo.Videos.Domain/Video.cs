namespace FiapVideo.Videos.Domain;

public class Video
{
    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string NomeOriginal { get; private set; } = string.Empty;
    public string ChaveOriginal { get; private set; } = string.Empty;
    public string? ChaveZip { get; private set; }
    public long TamanhoBytes { get; private set; }
    public StatusVideo Status { get; private set; }
    public string? MensagemErro { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }

    private Video() { }

    public static Video Criar(Guid id, Guid usuarioId, string nomeOriginal, string chaveOriginal, long tamanhoBytes)
    {
        if (id == Guid.Empty)
            throw new DomainException("Id do vídeo é obrigatório.");
        if (usuarioId == Guid.Empty)
            throw new DomainException("Usuário é obrigatório.");
        if (string.IsNullOrWhiteSpace(nomeOriginal))
            throw new DomainException("Nome do arquivo é obrigatório.");
        if (string.IsNullOrWhiteSpace(chaveOriginal))
            throw new DomainException("Chave do arquivo no storage é obrigatória.");
        if (tamanhoBytes <= 0)
            throw new DomainException("Tamanho do arquivo deve ser maior que zero.");

        var agora = DateTime.UtcNow;
        return new Video
        {
            Id = id,
            UsuarioId = usuarioId,
            NomeOriginal = nomeOriginal.Trim(),
            ChaveOriginal = chaveOriginal,
            TamanhoBytes = tamanhoBytes,
            Status = StatusVideo.Pendente,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
    }

    // Os três métodos abaixo devolvem false quando a transição não se aplica
    // (evento repetido ou fora de ordem), para o consumidor ser idempotente.
    public bool IniciarProcessamento()
    {
        if (Status != StatusVideo.Pendente) return false;
        Status = StatusVideo.Processando;
        AtualizadoEm = DateTime.UtcNow;
        return true;
    }

    public bool Concluir(string chaveZip)
    {
        if (string.IsNullOrWhiteSpace(chaveZip))
            throw new DomainException("Chave do zip é obrigatória.");
        if (Status is StatusVideo.Concluido or StatusVideo.Erro) return false;

        ChaveZip = chaveZip;
        MensagemErro = null;
        Status = StatusVideo.Concluido;
        AtualizadoEm = DateTime.UtcNow;
        return true;
    }

    public bool Falhar(string? mensagem)
    {
        if (Status is StatusVideo.Concluido or StatusVideo.Erro) return false;

        var texto = string.IsNullOrWhiteSpace(mensagem) ? "Erro no processamento." : mensagem.Trim();
        MensagemErro = texto.Length > 1000 ? texto[..1000] : texto;
        Status = StatusVideo.Erro;
        AtualizadoEm = DateTime.UtcNow;
        return true;
    }
}
