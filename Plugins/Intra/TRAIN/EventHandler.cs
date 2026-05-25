using Amazon.DynamoDBv2.Model;
using FormEventHandler;

namespace Intra.TRAIN;

public class EventHandler : GenericFormHandler
{
    public EventHandler() { }
    public EventHandler(HandlerContext context) : base(context) { }

    public override async Task Form_onInit(bool loadFromDb)
    {
        await base.Form_onInit(loadFromDb);
        await PopulateTypeOptions();
    }

    public override async Task Form_onAfterSave()
        => cmd.SuccessMessage(Translate("record.submit.success"));

    public override async Task Form_onClick(string fieldName)
    {
        if (fieldName == "btnaddtype")
        {
            string? pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString();
            if (!string.IsNullOrEmpty(pluginCode))
                cmd.ShowRecord(pluginCode, "TYPE", null);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────────

    private async Task PopulateTypeOptions()
    {
        if (_dynamoClient == null || _dbTableName == null) return;

        string pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString() ?? "";

        var queryRequest = new QueryRequest
        {
            TableName = _dbTableName,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"{pluginCode}#TYPE" } }
            }
        };

        var options = new Dictionary<string, string>();
        QueryResponse response;
        do
        {
            response = await _dynamoClient.QueryAsync(queryRequest);
            foreach (var item in response.Items)
            {
                if (!item.TryGetValue("value", out var nameVal) || string.IsNullOrEmpty(nameVal.S))
                    continue;
                options[nameVal.S] = nameVal.S;
            }
            queryRequest.ExclusiveStartKey = response.LastEvaluatedKey;
        }
        while (response.LastEvaluatedKey?.Count > 0);

        cmd.PopulateSelectBoxList("type", options);
    }
}
