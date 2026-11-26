using System.Diagnostics;
using System.Globalization;

namespace FiapVideo.Worker;

public interface IFrameExtractor
{
    Task<int> ExtrairAsync(string videoPath, string pastaSaida, CancellationToken ct);
}

public class FfmpegFrameExtractor(ProcessingOptions options, ILogger<FfmpegFrameExtractor> logger) : IFrameExtractor
{
    public async Task<int> ExtrairAsync(string videoPath, string pastaSaida, CancellationToken ct)
    {
        Directory.CreateDirectory(pastaSaida);
        var intervalo = options.FrameIntervalSeconds.ToString(CultureInfo.InvariantCulture);

        var psi = new ProcessStartInfo
        {
            FileName = options.FfmpegPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in new[]
        {
            "-hide_banner", "-loglevel", "error", "-nostdin", "-y",
            "-i", videoPath,
            "-vf", $"fps=1/{intervalo}",
            "-q:v", "2",
            Path.Combine(pastaSaida, "frame_%05d.jpg")
        })
            psi.ArgumentList.Add(arg);

        using var processo = new Process { StartInfo = psi };
        processo.Start();

        var erroTask = processo.StandardError.ReadToEndAsync(ct);
        var saidaTask = processo.StandardOutput.ReadToEndAsync(ct);

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromMinutes(options.TimeoutMinutes));

        try
        {
            await processo.WaitForExitAsync(limite.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            TentarEncerrar(processo);
            throw new ProcessamentoException($"Tempo limite de {options.TimeoutMinutes} minuto(s) excedido no processamento.");
        }
        catch (OperationCanceledException)
        {
            TentarEncerrar(processo);
            throw;
        }

        var erro = await erroTask;
        await saidaTask;

        if (processo.ExitCode != 0)
        {
            logger.LogWarning("ffmpeg terminou com código {Codigo}: {Erro}", processo.ExitCode, erro.Trim());
            throw new ProcessamentoException("O arquivo enviado não é um vídeo válido ou está corrompido.");
        }

        var total = Directory.EnumerateFiles(pastaSaida, "*.jpg").Count();
        if (total == 0)
            throw new ProcessamentoException("Nenhum frame foi extraído do vídeo.");
        return total;
    }

    private static void TentarEncerrar(Process processo)
    {
        try
        {
            if (!processo.HasExited) processo.Kill(entireProcessTree: true);
        }
        catch
        {
            // o processo já terminou
        }
    }
}
