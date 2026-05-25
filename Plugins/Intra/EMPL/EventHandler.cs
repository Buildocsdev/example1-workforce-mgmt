using Amazon.DynamoDBv2.Model;
using FormEventHandler;

namespace Intra.EMPL;

public class EventHandler : GenericFormHandler
{
    public EventHandler() { }
    public EventHandler(HandlerContext context) : base(context) { }

    public override async Task Form_onInit(bool loadFromDb)
    {
        await base.Form_onInit(loadFromDb);
        PopulateStatusOptions();
    }

    public override async Task Form_onAfterSave()
        => cmd.SuccessMessage(Translate("record.submit.success"));


    // ── Delete lifecycle ─────────────────────────────────────────────────────────

    public override Task Form_onBeforeDelete()
    {
        // Throw UserWarningException here to block deletion with a user-visible message.
        return Task.CompletedTask;
    }

    public override async Task Form_onAfterDelete()
        => cmd.NotificationMessage(Translate("record.delete.success"));

    // ── Table widget events ───────────────────────────────────────────────────────────

    public virtual async Task certificatestbl_onTableCreateRecordEvent()
    {
        await SaveCurrentAndUpdateOriginator();

        string? pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString();

        if (string.IsNullOrEmpty(pluginCode))
            return;

        cmd.ShowRecord(pluginCode, "CERT", null);
    }

    public async Task<List<Dictionary<string, string>>> certificatestbl_onTableLoadData()
    {
        var result = new List<Dictionary<string, string>>();
        if (_dynamoClient == null || _dbTableName == null) return result;

        string pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString() ?? "";
        string guid = contextHandlerInstance.Get("guid")?.ToString() ?? "";

        var queryRequest = new QueryRequest
        {
            TableName = _dbTableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk",       new AttributeValue { S = $"{pluginCode}#CERT" } },
                { ":skPrefix", new AttributeValue { S = $"{guid}#" } }
            }
        };

        QueryResponse response;
        do
        {
            response = await _dynamoClient.QueryAsync(queryRequest);
            var systemKeys = new HashSet<string> { "updatedAt" };
            foreach (var item in response.Items)
            {
                var row = new Dictionary<string, string>();

                if (item.TryGetValue("PK", out var pk)) row["_pk"] = pk.S;
                if (item.TryGetValue("SK", out var sk)) row["_sk"] = sk.S;

                foreach (var kv in item)
                    if (!systemKeys.Contains(kv.Key) && kv.Key != "PK" && kv.Key != "SK" && kv.Value.S != null)
                        row[kv.Key] = kv.Value.S;

                row["_plugincode"] = row["pluginCode"];
                row["_code"] = row["formCode"];

                result.Add(row);
            }
            queryRequest.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey?.Count > 0);

        return result;
    }

    public virtual async Task trainingtbl_onTableCreateRecordEvent()
    {
        await SaveCurrentAndUpdateOriginator();

        string? pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString();

        if (string.IsNullOrEmpty(pluginCode))
            return;

        cmd.ShowRecord(pluginCode, "TRAIN", null);
    }

    public async Task<List<Dictionary<string, string>>> trainingtbl_onTableLoadData()
    {
        var result = new List<Dictionary<string, string>>();
        if (_dynamoClient == null || _dbTableName == null) return result;

        string pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString() ?? "";
        string guid = contextHandlerInstance.Get("guid")?.ToString() ?? "";

        var queryRequest = new QueryRequest
        {
            TableName = _dbTableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk",       new AttributeValue { S = $"{pluginCode}#TRAIN" } },
                { ":skPrefix", new AttributeValue { S = $"{guid}#" } }
            }
        };

        QueryResponse response;
        do
        {
            response = await _dynamoClient.QueryAsync(queryRequest);
            var systemKeys = new HashSet<string> { "updatedAt" };
            foreach (var item in response.Items)
            {
                var row = new Dictionary<string, string>();

                if (item.TryGetValue("PK", out var pk)) row["_pk"] = pk.S;
                if (item.TryGetValue("SK", out var sk)) row["_sk"] = sk.S;

                foreach (var kv in item)
                    if (!systemKeys.Contains(kv.Key) && kv.Key != "PK" && kv.Key != "SK" && kv.Value.S != null)
                        row[kv.Key] = kv.Value.S;

                row["_plugincode"] = row["pluginCode"];
                row["_code"] = row["formCode"];

                result.Add(row);
            }
            queryRequest.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey?.Count > 0);

        return result;
    }




    // ── Private helpers ──────────────────────────────────────────────────────────

    private void PopulateStatusOptions()
    {
        cmd.PopulateSelectBoxList("status", new Dictionary<string, string>
        {
            { "Draft",       Translate("Draft") },
            { "Active",      Translate("Active") },
            { "In Progress", Translate("In Progress") },
            { "Completed",   Translate("Completed") },
            { "Cancelled",   Translate("Cancelled") }
        });
    }

}
