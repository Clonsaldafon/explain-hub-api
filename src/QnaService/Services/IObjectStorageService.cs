using Microsoft.AspNetCore.Http;

namespace QnaService.Services;

public interface IObjectStorageService
{
    Task EnsureBucketAsync(CancellationToken ct);
    Task<StoredObject> UploadAsync(IFormFile file, string folder, CancellationToken ct);
    Task<string> GetReadUrlAsync(string objectName, CancellationToken ct);
    Task DeleteAsync(string objectName, CancellationToken ct);
    bool TryValidate(IFormFile file, out string error);
}

public record StoredObject(
    string ObjectName,
    string FileName,
    string ContentType,
    long Size,
    string Url);
