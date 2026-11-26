namespace FiapVideo.Worker;

// Falha definitiva: reenviar a mesma mensagem não vai adiantar (vídeo inválido, arquivo inexistente).
public class ProcessamentoException(string message) : Exception(message);
