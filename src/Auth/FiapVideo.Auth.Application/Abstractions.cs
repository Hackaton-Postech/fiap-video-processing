using FiapVideo.Auth.Domain;

namespace FiapVideo.Auth.Application;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct);
    Task AdicionarAsync(Usuario usuario, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string senha);
    bool Verificar(string senha, string hash);
}

public record TokenGerado(string Token, DateTime ExpiraEmUtc);

public interface ITokenGenerator
{
    TokenGerado Gerar(Usuario usuario);
}
