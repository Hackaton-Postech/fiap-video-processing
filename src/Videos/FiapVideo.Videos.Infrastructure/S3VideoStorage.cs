using Amazon.S3;
using Amazon.S3.Model;
using FiapVideo.Videos.Application;

namespace FiapVideo.Videos.Infrastructure;

public class S3VideoStorage(IAmazonS3 s3, StorageOptions options) : IVideoStorage
{
    public async Task EnviarAsync(string chave, Stream conteudo, string contentType, CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = options.Bucket,
            Key = chave,
            InputStream = conteudo,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            AutoCloseStream = false
        };
        await s3.PutObjectAsync(request, ct);
    }

    public async Task<Stream> BaixarAsync(string chave, CancellationToken ct)
    {
        var resposta = await s3.GetObjectAsync(options.Bucket, chave, ct);
        return resposta.ResponseStream;
    }
}
