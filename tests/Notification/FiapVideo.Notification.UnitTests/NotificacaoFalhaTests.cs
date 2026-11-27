namespace FiapVideo.Notification.UnitTests;

public class NotificacaoFalhaTests
{
    private static VideoFalhou Evento(
        string nome = "Pedro",
        string email = "pedro@teste.com",
        string arquivo = "ferias.mp4",
        string motivo = "O arquivo enviado não é um vídeo válido.") =>
        new(Guid.NewGuid(), Guid.NewGuid(), email, nome, arquivo, motivo, DateTime.UtcNow);

    [Fact]
    public void Montar_UsaOEmailDoUsuarioEOAssuntoComONomeDoArquivo()
    {
        var mensagem = NotificacaoFalha.Montar(Evento());

        Assert.Equal("pedro@teste.com", mensagem.Para);
        Assert.Equal("Pedro", mensagem.NomePara);
        Assert.Equal("Falha ao processar o vídeo \"ferias.mp4\"", mensagem.Assunto);
    }

    [Fact]
    public void Montar_CorpoTemSaudacaoMotivoECodigoDeReferencia()
    {
        var evento = Evento(motivo: "Arquivo corrompido.");

        var mensagem = NotificacaoFalha.Montar(evento);

        Assert.StartsWith("Olá, Pedro!", mensagem.Corpo, StringComparison.Ordinal);
        Assert.Contains("Motivo: Arquivo corrompido.", mensagem.Corpo);
        Assert.Contains("\"ferias.mp4\"", mensagem.Corpo);
        Assert.Contains(evento.VideoId.ToString(), mensagem.Corpo);
    }

    [Fact]
    public void Montar_SemNome_UsaSaudacaoGenerica()
    {
        var mensagem = NotificacaoFalha.Montar(Evento(nome: "  "));

        Assert.StartsWith("Olá!", mensagem.Corpo, StringComparison.Ordinal);
        Assert.Equal(string.Empty, mensagem.NomePara);
    }

    [Fact]
    public void Montar_MotivoVazio_UsaTextoPadrao()
    {
        var mensagem = NotificacaoFalha.Montar(Evento(motivo: ""));

        Assert.Contains("Motivo: Erro no processamento.", mensagem.Corpo);
    }

    [Fact]
    public void Montar_RemoveQuebrasDeLinhaDoAssunto()
    {
        var mensagem = NotificacaoFalha.Montar(Evento(arquivo: "video\r\nBcc: alguem@x.com.mp4"));

        Assert.False(mensagem.Assunto.Contains('\r'));
        Assert.False(mensagem.Assunto.Contains('\n'));
    }

    [Fact]
    public void Montar_NomeDeArquivoMuitoLongo_EhTruncadoNoAssunto()
    {
        var mensagem = NotificacaoFalha.Montar(Evento(arquivo: new string('a', 300) + ".mp4"));

        Assert.True(mensagem.Assunto.Length <= 150);
    }

    [Fact]
    public void Montar_MotivoMuitoLongo_EhTruncadoEm500Caracteres()
    {
        var mensagem = NotificacaoFalha.Montar(Evento(motivo: new string('x', 800)));

        Assert.Contains(new string('x', 500), mensagem.Corpo);
        Assert.DoesNotContain(new string('x', 501), mensagem.Corpo);
    }

    [Fact]
    public void Montar_RemoveEspacosDoEmail()
    {
        var mensagem = NotificacaoFalha.Montar(Evento(email: "  pedro@teste.com "));

        Assert.Equal("pedro@teste.com", mensagem.Para);
    }
}
