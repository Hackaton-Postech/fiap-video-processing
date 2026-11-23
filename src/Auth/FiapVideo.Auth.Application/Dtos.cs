namespace FiapVideo.Auth.Application;

public record RegistrarRequest(string Nome, string Email, string Senha);

public record LoginRequest(string Email, string Senha);

public record UsuarioResponse(Guid Id, string Nome, string Email);

public record TokenResponse(string Token, DateTime ExpiraEmUtc);
