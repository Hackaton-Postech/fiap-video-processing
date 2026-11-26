using Amazon.Runtime;
using Amazon.S3;
using FiapVideo.Worker;

var builder = Host.CreateApplicationBuilder(args);

var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
var rabbit = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqOptions>() ?? new RabbitMqOptions();
var processamento = builder.Configuration.GetSection("Processing").Get<ProcessingOptions>() ?? new ProcessingOptions();

if (string.IsNullOrWhiteSpace(storage.ServiceUrl) || string.IsNullOrWhiteSpace(storage.AccessKey) ||
    string.IsNullOrWhiteSpace(storage.SecretKey))
    throw new InvalidOperationException("Storage:ServiceUrl, Storage:AccessKey e Storage:SecretKey são obrigatórios.");
if (string.IsNullOrWhiteSpace(rabbit.User) || string.IsNullOrWhiteSpace(rabbit.Password))
    throw new InvalidOperationException("RabbitMq:User e RabbitMq:Password são obrigatórios.");

builder.Services.AddSingleton(storage);
builder.Services.AddSingleton(rabbit);
builder.Services.AddSingleton(processamento);

builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
    new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
    new AmazonS3Config
    {
        ServiceURL = storage.ServiceUrl,
        ForcePathStyle = true,
        AuthenticationRegion = storage.Region,
        RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
        ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
    }));
builder.Services.AddSingleton<IVideoStorage, S3VideoStorage>();
builder.Services.AddSingleton<IFrameExtractor, FfmpegFrameExtractor>();
builder.Services.AddSingleton<VideoProcessor>();
builder.Services.AddHostedService<VideoConsumer>();

await builder.Build().RunAsync();
