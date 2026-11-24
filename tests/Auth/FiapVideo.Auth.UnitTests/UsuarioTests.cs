using FiapVideo.Auth.Domain;

namespace FiapVideo.Auth.UnitTests;

public class UsuarioTests
{
    [Fact]
    public void Criar_DadosValidos_NormalizaNomeEEmail()
    {
        var u = Usuario.Criar("  Pedro  ", "  Pedro@Teste.COM ", "hash");

        Assert.NotEqual(Guid.Empty, u.Id);
        Assert.Equal("Pedro", u.Nome);
        Assert.Equal("pedro@teste.com", u.Email);
        Assert.Equal("hash", u.SenhaHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_LancaDomainException(string nome)
    {
        Assert.Throws<DomainException>(() => Usuario.Criar(nome, "a@b.com", "hash"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("semarroba")]
    public void Criar_EmailInvalido_LancaDomainException(string email)
    {
        Assert.Throws<DomainException>(() => Usuario.Criar("Pedro", email, "hash"));
    }

    [Fact]
    public void Criar_SenhaHashVazio_LancaDomainException()
    {
        Assert.Throws<DomainException>(() => Usuario.Criar("Pedro", "a@b.com", ""));
    }
}
