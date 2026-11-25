using Amazon.Runtime;
using Amazon.S3;
using FiapVideo.Videos.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FiapVideo.Videos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVideosInfrastructure(
        this IServiceCollection services,
        string connectionString,
        StorageOptions storage,
        RabbitMqOptions rabbit)
    {
        if (string.IsNullOrWhiteSpace(storage.ServiceUrl) || string.IsNullOrWhiteSpace(storage.AccessKey) ||
            string.IsNullOrWhiteSpace(storage.SecretKey))
            throw new InvalidOperationException("Storage:ServiceUrl, Storage:AccessKey e Storage:SecretKey são obrigatórios.");
        if (string.IsNullOrWhiteSpace(rabbit.User) || string.IsNullOrWhiteSpace(rabbit.Password))
            throw new InvalidOperationException("RabbitMq:User e RabbitMq:Password são obrigatórios.");

        services.AddSingleton(storage);
        services.AddSingleton(rabbit);

        services.AddDbContext<VideosDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IVideoRepository, VideoRepository>();

        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = storage.ServiceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = storage.Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            }));
        services.AddSingleton<IVideoStorage, S3VideoStorage>();

        services.AddHostedService<OutboxPublisher>();
        services.AddScoped<VideoService>();
        return services;
    }
}
