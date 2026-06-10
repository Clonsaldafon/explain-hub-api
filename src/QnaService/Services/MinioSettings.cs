namespace QnaService.Services;

public class MinioSettings
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string? PublicEndpoint { get; set; }
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string BucketName { get; set; } = "qna-media";
    public bool UseSsl { get; set; }
    public int PresignedUrlExpirySeconds { get; set; } = 3600;
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024;
    public string[] AllowedContentTypes { get; set; } =
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "video/mp4",
        "video/webm",
        "video/quicktime"
    };
}
