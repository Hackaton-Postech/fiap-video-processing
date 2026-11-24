using System.IdentityModel.Tokens.Jwt;
using FiapVideo.Auth.Domain;
using FiapVideo.Auth.Infrastructure;

namespace FiapVideo.Auth.UnitTests;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _sut = new();

    [Fact]
    public void Hash_GeraValorDiferenteDaSenhaEVerificaCorretamente()
    {
        var hash = _sut.Hash("Senha12345");

        Assert.NotEqual("Senha12345", hash);
        Assert.True(_sut.Verificar("Senha12345", hash));
    }

    [Fact]
    public void Verificar_SenhaErrada_RetornaFalse()
    {
        var hash = _sut.Hash("Senha12345");

        Assert.False(_sut.Verificar("outra-senha", hash));
    }
}

public class JwtTokenGeneratorTests
{
    private static readonly JwtOptions Opcoes = new()
    {
        Secret = "chave-de-teste-com-mais-de-32-caracteres-123",
        ExpiraEmMinutos = 30
    };

    [Fact]
    public void Gerar_IncluiClaimsEExpiracao()
    {
        var usuario = Usuario.Criar("Pedro", "pedro@teste.com", "hash");

        var token = new JwtTokenGenerator(Opcoes).Gerar(usuario);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);

        Assert.Equal(usuario.Id.ToString(), jwt.Subject);
        Assert.Equal("pedro@teste.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("Pedro", jwt.Claims.First(c => c.Type == "name").Value);
        Assert.Equal(Opcoes.Issuer, jwt.Issuer);
        Assert.True(token.ExpiraEmUtc > DateTime.UtcNow.AddMinutes(29));
        Assert.True(token.ExpiraEmUtc <= DateTime.UtcNow.AddMinutes(30));
    }
}
