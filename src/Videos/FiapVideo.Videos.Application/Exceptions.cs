namespace FiapVideo.Videos.Application;

public class ValidacaoException(string message) : Exception(message);

public class NaoEncontradoException(string message) : Exception(message);

public class ConflitoException(string message) : Exception(message);
