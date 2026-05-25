namespace FileStorage;

public interface IFileStorageProvider
{
    string GetBucketName();
    string GetTenantId();
    string GetFileUploadPath(string? tenantId, string prefix, DateTime currentDate);
    Task<string> UploadFileAsync(string filePath, Stream fileStream, string? contentType = null);
    Task<bool> DeleteFileAsync(string filePath, string bucketName);
    Task<Stream?> GetFileStreamAsync(string filePath, string bucketName);
    Task<string> GetPresignedUrlAsync(string filePath, string bucketName, double hours = 1);
    Task<FileMetadata?> GetFileMetadataAsync(string filePath, string bucketName);
    Task<byte[]?> GetFileBytesAsync(string filePath, string bucketName);
    Task WriteInChunksAsync(Stream destination, Stream source, int bufferSize = 81920);
    string GetMimeType(string fileName);
}

public record FileMetadata(long ContentLength, string ContentType);
