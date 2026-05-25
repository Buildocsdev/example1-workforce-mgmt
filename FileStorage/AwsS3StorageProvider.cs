using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace FileStorage;

public class AwsS3StorageProvider : IFileStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _tenantId;

    public AwsS3StorageProvider(IConfiguration configuration)
    {
        var s3 = configuration.GetSection("AWS:S3");
        string region = configuration["AWS:Region"] ?? "us-east-1";
        _bucketName = s3["BucketName"] ?? "buildocs-demo";
        _tenantId = s3["TenantId"] ?? "default";

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            ForcePathStyle = bool.TryParse(s3["ForcePathStyle"], out var fp) && fp
        };
        string? svcUrl = s3["ServiceURL"];
        if (!string.IsNullOrEmpty(svcUrl))
            config.ServiceURL = svcUrl;

        _s3Client = new AmazonS3Client(s3["AccessKey"], s3["SecretKey"], config);
    }

    public string GetBucketName() => _bucketName;
    public string GetTenantId() => _tenantId;

    public string GetFileUploadPath(string? tenantId, string prefix, DateTime currentDate)
        => $"{tenantId}/{prefix}/{currentDate:yyyy/MM/dd}".Replace('\\', '/');

    public async Task<string> UploadFileAsync(string filePath, Stream fileStream, string? contentType = null)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = filePath,
            InputStream = fileStream,
            ContentType = contentType ?? "application/octet-stream",
            AutoCloseStream = false
        };

        try
        {
            await _s3Client.PutObjectAsync(request);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            await CreateBucketAsync();
            if (fileStream.CanSeek) fileStream.Position = 0;
            await _s3Client.PutObjectAsync(request);
        }

        return $"{_bucketName}:{filePath}";
    }

    private async Task CreateBucketAsync()
    {
        try
        {
            await _s3Client.PutBucketAsync(new PutBucketRequest
            {
                BucketName = _bucketName,
                UseClientRegion = true
            });
        }
        catch (AmazonS3Exception ex) when (
            ex.ErrorCode == "BucketAlreadyExists" ||
            ex.ErrorCode == "BucketAlreadyOwnedByYou")
        {
            // already exists — safe to continue
        }
    }

    public async Task<bool> DeleteFileAsync(string filePath, string bucketName)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucketName,
                Key = filePath
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Stream?> GetFileStreamAsync(string filePath, string bucketName)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucketName,
                Key = filePath
            });
            return response.ResponseStream;
        }
        catch
        {
            return null;
        }
    }

    public Task<string> GetPresignedUrlAsync(string filePath, string bucketName, double hours = 1)
    {
        string url = _s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = filePath,
            Expires = DateTime.UtcNow.AddHours(hours)
        });
        return Task.FromResult(url);
    }

    public async Task<FileMetadata?> GetFileMetadataAsync(string filePath, string bucketName)
    {
        try
        {
            var response = await _s3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = filePath
            });
            return new FileMetadata(response.ContentLength, response.Headers.ContentType ?? "application/octet-stream");
        }
        catch
        {
            return null;
        }
    }

    public async Task<byte[]?> GetFileBytesAsync(string filePath, string bucketName)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucketName,
                Key = filePath
            });
            using var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }

    public async Task WriteInChunksAsync(Stream destination, Stream source, int bufferSize = 81920)
    {
        byte[] buffer = new byte[bufferSize];
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            await destination.WriteAsync(buffer, 0, bytesRead);
    }

    public string GetMimeType(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf"  => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"  => "image/png",
            ".gif"  => "image/gif",
            ".webp" => "image/webp",
            ".svg"  => "image/svg+xml",
            ".mp4"  => "video/mp4",
            ".webm" => "video/webm",
            ".mp3"  => "audio/mpeg",
            ".wav"  => "audio/wav",
            ".doc"  => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls"  => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt"  => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".zip"  => "application/zip",
            ".txt"  => "text/plain",
            ".csv"  => "text/csv",
            ".json" => "application/json",
            ".xml"  => "application/xml",
            _ => "application/octet-stream"
        };
    }
}
