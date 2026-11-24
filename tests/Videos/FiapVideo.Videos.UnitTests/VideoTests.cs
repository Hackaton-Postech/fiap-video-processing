using FiapVideo.Videos.Domain;

namespace FiapVideo.Videos.UnitTests;

public class VideoTests
{
    private static Video NovoVideo() =>
        Video.Criar(Guid.NewGuid(), Guid.NewGuid(), "video.mp4", "u/v/original.mp4", 1024);

    [Fact]
    public void Criar_DadosValidos_IniciaComoPendente()
    {
        var id = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();

        var v = Video.Criar(id, usuarioId, "  video.mp4 ", "u/v/original.mp4", 1024);

        Assert.Equal(id, v.Id);
        Assert.Equal(usuarioId, v.UsuarioId);
        Assert.Equal("video.mp4", v.NomeOriginal);
        Assert.Equal(StatusVideo.Pendente, v.Status);
        Assert.Null(v.ChaveZip);
        Assert.Null(v.MensagemErro);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        Assert.Throws<DomainException>(() =>
            Video.Criar(Guid.NewGuid(), Guid.NewGuid(), nome, "chave", 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_TamanhoInvalido_LancaDomainException(long tamanho)
    {
        Assert.Throws<DomainException>(() =>
            Video.Criar(Guid.NewGuid(), Guid.NewGuid(), "video.mp4", "chave", tamanho));
    }

    [Fact]
    public void Criar_UsuarioVazio_LancaDomainException()
    {
        Assert.Throws<DomainException>(() =>
            Video.Criar(Guid.NewGuid(), Guid.Empty, "video.mp4", "chave", 10));
    }

    [Fact]
    public void IniciarProcessamento_Pendente_MudaStatusERepetidoRetornaFalse()
    {
        var v = NovoVideo();

        Assert.True(v.IniciarProcessamento());
        Assert.Equal(StatusVideo.Processando, v.Status);
        Assert.False(v.IniciarProcessamento());
    }

    [Fact]
    public void Concluir_EmProcessamento_DefineZipEStatus()
    {
        var v = NovoVideo();
        v.IniciarProcessamento();

        Assert.True(v.Concluir("u/v/frames.zip"));

        Assert.Equal(StatusVideo.Concluido, v.Status);
        Assert.Equal("u/v/frames.zip", v.ChaveZip);
        Assert.Null(v.MensagemErro);
    }

    [Fact]
    public void Concluir_Repetido_RetornaFalse()
    {
        var v = NovoVideo();
        v.Concluir("u/v/frames.zip");

        Assert.False(v.Concluir("outro.zip"));
        Assert.Equal("u/v/frames.zip", v.ChaveZip);
    }

    [Fact]
    public void Concluir_ChaveVazia_LancaDomainException()
    {
        Assert.Throws<DomainException>(() => NovoVideo().Concluir(" "));
    }

    [Fact]
    public void Falhar_DefineErroEMensagem()
    {
        var v = NovoVideo();

        Assert.True(v.Falhar("  arquivo corrompido "));

        Assert.Equal(StatusVideo.Erro, v.Status);
        Assert.Equal("arquivo corrompido", v.MensagemErro);
    }

    [Fact]
    public void Falhar_MensagemLonga_EhTruncada()
    {
        var v = NovoVideo();

        v.Falhar(new string('x', 1500));

        Assert.Equal(1000, v.MensagemErro!.Length);
    }

    [Fact]
    public void Falhar_SemMensagem_UsaTextoPadrao()
    {
        var v = NovoVideo();

        v.Falhar(null);

        Assert.Equal("Erro no processamento.", v.MensagemErro);
    }

    [Fact]
    public void Falhar_AposConcluido_RetornaFalseEMantemStatus()
    {
        var v = NovoVideo();
        v.Concluir("u/v/frames.zip");

        Assert.False(v.Falhar("tarde demais"));
        Assert.Equal(StatusVideo.Concluido, v.Status);
    }
}
