using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;

namespace SampleApi.Services;

public interface IAwsDemoService
{
    Task<(bool Success, string Message)> RunDemoAsync();
}

public class AwsDemoService : IAwsDemoService
{
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly IAmazonS3 _s3Client;
    private readonly ILogger<AwsDemoService> _logger;

    public AwsDemoService(IAmazonDynamoDB dynamoClient, IAmazonS3 s3Client, ILogger<AwsDemoService> logger)
    {
        _dynamoClient = dynamoClient;
        _s3Client     = s3Client;
        _logger       = logger;
    }

    public async Task<(bool Success, string Message)> RunDemoAsync()
    {
        try
        {
            await SaveToDynamoDbAsync();
            await UploadToS3Async();
            return (true, "Saved to DynamoDB and uploaded to S3");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Demo connectivity check failed");
            return (false, $"Error: {ex.Message}");
        }
    }

    private async Task SaveToDynamoDbAsync()
    {
        const string tableName = "DemoItems";

        try
        {
            await _dynamoClient.DescribeTableAsync(new DescribeTableRequest { TableName = tableName });
        }
        catch (ResourceNotFoundException)
        {
            await _dynamoClient.CreateTableAsync(new CreateTableRequest
            {
                TableName            = tableName,
                BillingMode          = BillingMode.PAY_PER_REQUEST,
                KeySchema            = new List<KeySchemaElement>
                {
                    new KeySchemaElement("Id", KeyType.HASH)
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition("Id", ScalarAttributeType.S)
                }
            });
        }

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = tableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["Id"]        = new AttributeValue { S = Guid.NewGuid().ToString() },
                ["Timestamp"] = new AttributeValue { S = DateTime.UtcNow.ToString("o") },
                ["Message"]   = new AttributeValue { S = "Test record from .NET 8 API" }
            }
        });

        _logger.LogInformation("Saved test item to DynamoDB table {Table}", tableName);
    }

    private async Task UploadToS3Async()
    {
        const string bucketName = "demo-bucket";
        const string keyName    = "hello.txt";
        const string content    = "Hello from .NET 8 API!";

        try
        {
            await _s3Client.PutBucketAsync(bucketName);
        }
        catch (AmazonS3Exception ex) when (
            ex.ErrorCode == "BucketAlreadyExists" ||
            ex.ErrorCode == "BucketAlreadyOwnedByYou") { }

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName  = bucketName,
            Key         = keyName,
            ContentType = "text/plain",
            InputStream = stream
        });

        _logger.LogInformation("Uploaded test file to S3 {Bucket}/{Key}", bucketName, keyName);
    }
}
