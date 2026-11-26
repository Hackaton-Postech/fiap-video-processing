using System.Net;
using Amazon.S3;
using Amazon.S3.Model;

namespace FiapVideo.Worker;

public interface IVideoStorage
{
    Task BaixarParaArquivoAsync(string chave, string destino, CancellationToken ct);
    Task EnviarArquivoAsync(string chave, string arquivo, string contentType, CancellationToken ct);
}

public class S3VideoStorage(IAmazonS3 s3, StorageOptions options) : IVideoStorage
{
    public async Task BaixarParaArquivoAsync(string chave, string destino, CancellationToken ct)
    {
        try
        {
            using var resposta = await s3.GetObjectAsync(options.Bucket, chave, ct);
            await using var arquivo = File.Create(destino);
            await resposta.ResponseStream.CopyToAsync(arquivo, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ProcessamentoException("Arquivo original não encontrado no storage.");
        }
    }

    public async Task EnviarArquivoAsync(string chave, string arquivo, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = chave,
            FilePath = arquivo,
            ContentType = contentType
        };
        await s3.PutObjectAsync(request, ct);
    }
}
