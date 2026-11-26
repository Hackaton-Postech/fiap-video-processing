using System.IO.Compression;

namespace FiapVideo.Worker;

public record ResultadoProcessamento(string ChaveZip, int TotalFrames);

public class VideoProcessor(IVideoStorage storage, IFrameExtractor extrator)
{
    public async Task<ResultadoProcessamento> ProcessarAsync(VideoEnviado mensagem, CancellationToken ct)
    {
        // Pasta única por tentativa: se a mesma mensagem chegar duas vezes, uma não atrapalha a outra.
        var pasta = Path.Combine(Path.GetTempPath(), "fiapvideo", mensagem.VideoId.ToString("N"), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        try
        {
            var arquivoOriginal = Path.Combine(pasta, "original" + Path.GetExtension(mensagem.ChaveOriginal));
            var pastaFrames = Path.Combine(pasta, "frames");
            var arquivoZip = Path.Combine(pasta, "frames.zip");

            await storage.BaixarParaArquivoAsync(mensagem.ChaveOriginal, arquivoOriginal, ct);
            var total = await extrator.ExtrairAsync(arquivoOriginal, pastaFrames, ct);

            ZipFile.CreateFromDirectory(pastaFrames, arquivoZip, CompressionLevel.Fastest, includeBaseDirectory: false);

            var chaveZip = ChaveDoZip(mensagem.ChaveOriginal);
            await storage.EnviarArquivoAsync(chaveZip, arquivoZip, "application/zip", ct);
            return new ResultadoProcessamento(chaveZip, total);
        }
        finally
        {
            try
            {
                Directory.Delete(pasta, recursive: true);
            }
            catch
            {
                // limpeza best-effort
            }
        }
    }

    public static string ChaveDoZip(string chaveOriginal)
    {
        var i = chaveOriginal.LastIndexOf('/');
        return i < 0 ? "frames.zip" : chaveOriginal[..(i + 1)] + "frames.zip";
    }
}
