using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Collections.Concurrent;

namespace FormEventHandler;

public class DataRecord
{
    // Tracks tables already confirmed to exist so EnsureTableExistsAsync skips
    // the DescribeTable round-trip on every subsequent Save to the same table.
    private static readonly ConcurrentDictionary<string, bool> _knownTables = new();

    private Dictionary<string, object> _data = new();
    private string? _guid;
    private string? _projectGuid;

    private IAmazonDynamoDB? _dynamoClient;
    private string? _tableName;
    private string? _pluginCode;
    private string? _formCode;

    // ── Configuration ──────────────────────────────────────────────────────────

    public void Configure(IAmazonDynamoDB client, string tableName, string? pluginCode, string? formCode, string? guid)
    {
        _dynamoClient = client;
        _tableName    = tableName;
        _pluginCode   = pluginCode;
        _formCode     = formCode;
        if (!string.IsNullOrEmpty(guid) && !string.Equals(guid, "new", StringComparison.OrdinalIgnoreCase))
            _guid = guid;
    }

    // ── Data accessors ─────────────────────────────────────────────────────────

    public object? GetField(string fieldName)
        => _data.TryGetValue(fieldName, out var v) ? v : null;

    public void SetField(string fieldName, object value)
        => _data[fieldName] = value;

    public Dictionary<string, object> GetData() => new(_data);

    public void SetData(Dictionary<string, object>? data)
    {
        if (data != null) _data = new(data);
    }

    public void LinkFiles(string fieldName, List<string>? files)
    {
        _data[fieldName] = files != null
            ? System.Text.Json.JsonSerializer.Serialize(files)
            : string.Empty;
    }

    public string? GetRecordGuid() => _guid;
    public void SetRecordGuid(string? guid) => _guid = guid;

    public string? GetProjectGuid() => _projectGuid;
    public void SetProjectGuid(string? guid) => _projectGuid = guid;

    public bool IsNewByRequest()
        => string.IsNullOrEmpty(_guid)
        || string.Equals(_guid, "new", StringComparison.OrdinalIgnoreCase);

    // ── DynamoDB operations ────────────────────────────────────────────────────

    public virtual async Task Save()
    {
        if (_dynamoClient == null || _tableName == null) return;

        if (IsNewByRequest())
            _guid = Guid.NewGuid().ToString();

        await EnsureTableExistsAsync();

        var item = new Dictionary<string, AttributeValue>
        {
            ["PK"]         = new AttributeValue { S = $"{_pluginCode}#{_formCode}" },
            ["SK"]         = new AttributeValue { S = _guid! },
            ["pluginCode"] = new AttributeValue { S = _pluginCode ?? "" },
            ["formCode"]   = new AttributeValue { S = _formCode ?? "" },
            ["updatedAt"]  = new AttributeValue { S = DateTime.UtcNow.ToString("o") }
        };

        foreach (var kv in _data)
            if (kv.Value != null)
                item[kv.Key] = new AttributeValue { S = kv.Value.ToString() ?? "" };

        await _dynamoClient.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item      = item
        });
    }

    public virtual async Task Delete(bool cascade = false)
    {
        if (_dynamoClient == null || _tableName == null || string.IsNullOrEmpty(_guid)) return;

        await _dynamoClient.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{_pluginCode}#{_formCode}" },
                ["SK"] = new AttributeValue { S = _guid! }
            }
        });
    }

    public virtual async Task LoadRecord()
    {
        if (_dynamoClient == null || _tableName == null || string.IsNullOrEmpty(_guid)) return;

        var response = await _dynamoClient.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"{_pluginCode}#{_formCode}" },
                ["SK"] = new AttributeValue { S = _guid! }
            }
        });

        var systemKeys = new HashSet<string> { "PK", "SK", "pluginCode", "formCode", "updatedAt" };
        foreach (var kv in response.Item)
            if (!systemKeys.Contains(kv.Key) && kv.Value.S != null)
                _data[kv.Key] = kv.Value.S;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task EnsureTableExistsAsync()
    {
        if (_tableName == null || _knownTables.ContainsKey(_tableName)) return;

        try
        {
            await _dynamoClient!.DescribeTableAsync(new DescribeTableRequest { TableName = _tableName });
        }
        catch (ResourceNotFoundException)
        {
            await _dynamoClient!.CreateTableAsync(new CreateTableRequest
            {
                TableName            = _tableName,
                BillingMode          = BillingMode.PAY_PER_REQUEST,
                KeySchema            = new List<KeySchemaElement>
                {
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE)
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S)
                }
            });
        }

        _knownTables[_tableName] = true;
    }
}
