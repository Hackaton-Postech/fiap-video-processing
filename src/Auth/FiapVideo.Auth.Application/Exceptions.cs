namespace FiapVideo.Auth.Application;

public class ValidacaoException(string message) : Exception(message);

public class ConflitoException(string message) : Exception(message);

public class CredenciaisInvalidasException() : Exception("E-mail ou senha inválidos.");
