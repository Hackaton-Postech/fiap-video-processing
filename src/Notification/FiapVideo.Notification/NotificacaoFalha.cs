namespace FiapVideo.Notification;

public record MensagemEmail(string Para, string NomePara, string Assunto, string Corpo);

public static class NotificacaoFalha
{
    public static MensagemEmail Montar(VideoFalhou evento)
    {
        var arquivo = Limpar(evento.NomeOriginal, 120);
        var nome = Limpar(evento.Nome, 80);
        var motivo = Limpar(evento.Mensagem, 500);
        if (motivo.Length == 0) motivo = "Erro no processamento.";

        var saudacao = nome.Length > 0 ? $"Olá, {nome}!" : "Olá!";
        var assunto = $"Falha ao processar o vídeo \"{arquivo}\"";

        var corpo = string.Join("\n",
            saudacao,
            "",
            $"Não conseguimos processar o vídeo \"{arquivo}\".",
            "",
            $"Motivo: {motivo}",
            "",
            "Você pode enviar o arquivo novamente. Se o problema continuar, tente outro vídeo (.mp4, .avi, .mov, .mkv ou .webm).",
            "",
            $"Código de referência: {evento.VideoId}");

        return new MensagemEmail(evento.Email.Trim(), nome, assunto, corpo);
    }

    // Remove quebras de linha e outros caracteres de controle (evita injeção de cabeçalhos no e-mail).
    private static string Limpar(string? texto, int maximo)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        var limpo = new string(texto.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return limpo.Length > maximo ? limpo[..maximo] : limpo;
    }
}
