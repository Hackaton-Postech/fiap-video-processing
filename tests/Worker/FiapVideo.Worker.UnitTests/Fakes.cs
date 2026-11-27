using System.IO.Compression;

namespace FiapVideo.Worker.UnitTests;

public class FakeStorage : IVideoStorage
{
    public string? ChaveBaixada { get; private set; }
    public string? ArquivoBaixado { get; private set; }
    public string? ChaveEnviada { get; private set; }
    public string? ContentTypeEnviado { get; private set; }
    public List<string> EntradasDoZip { get; } = new();
    public Exception? ErroAoBaixar { get; set; }

    public Task BaixarParaArquivoAsync(string chave, string destino, CancellationToken ct)
    {
        if (ErroAoBaixar is not null) throw ErroAoBaixar;
        ChaveBaixada = chave;
        ArquivoBaixado = destino;
        File.WriteAllBytes(destino, new byte[] { 1, 2, 3 });
        return Task.CompletedTask;
    }

    public Task EnviarArquivoAsync(string chave, string arquivo, string contentType, CancellationToken ct)
    {
        ChaveEnviada = chave;
        ContentTypeEnviado = contentType;
        // O zip precisa ser lido agora: o processador apaga a pasta temporária ao terminar.
        using var zip = ZipFile.OpenRead(arquivo);
        EntradasDoZip.AddRange(zip.Entries.Select(e => e.FullName));
        return Task.CompletedTask;
    }
}

public class FakeExtractor : IFrameExtractor
{
    public int Frames { get; set; } = 3;
    public Exception? Erro { get; set; }
    public string? VideoRecebido { get; private set; }

    public Task<int> ExtrairAsync(string videoPath, string pastaSaida, CancellationToken ct)
    {
        if (Erro is not null) throw Erro;
        VideoRecebido = videoPath;
        Directory.CreateDirectory(pastaSaida);
        for (var i = 1; i <= Frames; i++)
            File.WriteAllBytes(Path.Combine(pastaSaida, $"frame_{i:D5}.jpg"), new byte[] { 1 });
        return Task.FromResult(Frames);
    }
}
