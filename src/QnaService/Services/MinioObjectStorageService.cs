using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace QnaService.Services;

public class MinioObjectStorageService : IObjectStorageService
{
    private readonly MinioSettings _settings;
    private readonly IMinioClient _client;
    private readonly IMinioClient _publicClient;
    private readonly ILogger<MinioObjectStorageService> _logger;

    public MinioObjectStorageService(IOptions<MinioSettings> options, ILogger<MinioObjectStorageService> logger)
    {
        _settings = options.Value;
        _logger = logger;
        _client = BuildClient(_settings.Endpoint);
        _publicClient = BuildClient(_settings.PublicEndpoint ?? _settings.Endpoint);
    }

    public async Task EnsureBucketAsync(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                var exists = await _client.BucketExistsAsync(
                    new BucketExistsArgs().WithBucket(_settings.BucketName),
                    ct);

                if (!exists)
                {
                    await _client.MakeBucketAsync(
                        new MakeBucketArgs().WithBucket(_settings.BucketName),
                        ct);
                }

                return;
            }
            catch (Exception ex) when (attempt < 10)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 10));
                _logger.LogWarning(
                    ex,
                    "MinIO bucket check failed on attempt {Attempt}. Retrying in {DelaySeconds} seconds",
                    attempt,
                    delay.TotalSeconds);

                await Task.Delay(delay, ct);
            }
        }

        var finalExists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_settings.BucketName),
            ct);

        if (!finalExists)
        {
            await _client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_settings.BucketName),
                ct);
        }
    }

    public async Task<StoredObject> UploadAsync(IFormFile file, string folder, CancellationToken ct)
    {
        if (!TryValidate(file, out var error))
            throw new InvalidOperationException(error);

        await EnsureBucketAsync(ct);

        var extension = Path.GetExtension(file.FileName);
        var objectName = $"{folder}/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
        var fileName = Path.GetFileName(file.FileName);

        await using var stream = file.OpenReadStream();
        await _client.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(file.Length)
                .WithContentType(file.ContentType),
            ct);

        var url = await GetReadUrlAsync(objectName, ct);
        return new StoredObject(objectName, fileName, file.ContentType, file.Length, url);
    }

    public Task<string> GetReadUrlAsync(string objectName, CancellationToken ct)
    {
        var expiry = Math.Clamp(_settings.PresignedUrlExpirySeconds, 60, 604800);
        return _publicClient.PresignedGetObjectAsync(
            new PresignedGetObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithExpiry(expiry));
    }

    public Task DeleteAsync(string objectName, CancellationToken ct)
    {
        return _client.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName),
            ct);
    }

    public bool TryValidate(IFormFile file, out string error)
    {
        if (file.Length <= 0)
        {
            error = "File is empty";
            return false;
        }

        if (file.Length > _settings.MaxFileSizeBytes)
        {
            error = $"File exceeds limit of {_settings.MaxFileSizeBytes} bytes";
            return false;
        }

        if (!_settings.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            error = "Only configured image and video content types are allowed";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private IMinioClient BuildClient(string endpoint)
    {
        return new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(_settings.AccessKey, _settings.SecretKey)
            .WithSSL(_settings.UseSsl)
            .Build();
    }
}
