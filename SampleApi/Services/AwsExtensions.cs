using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.S3;

namespace SampleApi.Services;

public static class AwsExtensions
{
    public static IServiceCollection AddDynamoDb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAmazonDynamoDB>(_ =>
        {
            var serviceUrl = configuration["AWS:DynamoDB:ServiceURL"] ?? "http://localhost:8000";
            var credentials = new BasicAWSCredentials("dummy", "dummy");
            return new AmazonDynamoDBClient(credentials, new AmazonDynamoDBConfig
            {
                ServiceURL           = serviceUrl,
                AuthenticationRegion = "us-east-1",
                Timeout              = TimeSpan.FromSeconds(30),
                MaxErrorRetry        = 0
            });
        });
        return services;
    }

    public static IServiceCollection AddS3(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAmazonS3>(_ =>
        {
            var s3 = configuration.GetSection("AWS:S3");
            var serviceUrl     = s3["ServiceURL"] ?? "http://localhost:9000";
            var forcePathStyle = bool.Parse(s3["ForcePathStyle"] ?? "true");
            var accessKey      = s3["AccessKey"] ?? "admin";
            var secretKey      = s3["SecretKey"] ?? "adminpassword";

            return new AmazonS3Client(
                new BasicAWSCredentials(accessKey, secretKey),
                new AmazonS3Config
                {
                    ServiceURL           = serviceUrl,
                    ForcePathStyle       = forcePathStyle,
                    AuthenticationRegion = "us-east-1",
                    Timeout              = TimeSpan.FromSeconds(30),
                    MaxErrorRetry        = 0
                });
        });
        return services;
    }
}
