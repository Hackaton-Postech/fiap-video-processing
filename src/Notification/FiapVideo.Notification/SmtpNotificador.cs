using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FiapVideo.Notification;

public interface INotificador
{
    Task EnviarAsync(MensagemEmail mensagem, CancellationToken ct);
}

public class SmtpNotificador(SmtpOptions options) : INotificador
{
    public async Task EnviarAsync(MensagemEmail mensagem, CancellationToken ct)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName, options.From));
        mime.To.Add(new MailboxAddress(mensagem.NomePara, mensagem.Para));
        mime.Subject = mensagem.Assunto;
        mime.Body = new TextPart("plain") { Text = mensagem.Corpo };

        using var smtp = new SmtpClient();
        var seguranca = options.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await smtp.ConnectAsync(options.Host, options.Port, seguranca, ct);
        if (!string.IsNullOrEmpty(options.User))
            await smtp.AuthenticateAsync(options.User, options.Password, ct);
        await smtp.SendAsync(mime, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}
