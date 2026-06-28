using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FormEventHandler;

public static class TableQueryHelper
{
    private static readonly JsonSerializerOptions _tokenSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static async Task<List<Dictionary<string, string>>> ExecutePagedQueryAsync(
        IAmazonDynamoDB dynamoClient,
        QueryRequest queryRequest,
        Func<Dictionary<string, AttributeValue>, Dictionary<string, string>> mapRow,
        RequestContext? requestContext,
        string qId = "")
    {
        var result = new List<Dictionary<string, string>>();
        var dataMeta = requestContext?.GetRequestData()?.DataTableMeta;
        bool serverPagination = dataMeta?.serverPaginationEnabled == true && dataMeta.rowsPerPage > 0;

        if (serverPagination)
        {
            int totalCount = await SetTotalRowCount(dynamoClient, queryRequest, requestContext!);

            bool scanForward;
            Dictionary<string, AttributeValue>? startKey;
            int queryLimit;

            if (dataMeta!.paginationDirection == "last")
            {
                scanForward = false;
                startKey    = null;
                queryLimit  = totalCount - (dataMeta.rowsPerPage * dataMeta.pageIndex);
            }
            else if (dataMeta.paginationDirection == "current")
            {
                queryLimit = dataMeta.lastRequestLimit > 0 ? dataMeta.lastRequestLimit : dataMeta.rowsPerPage;

                if (dataMeta.oppositeDirection == true)
                {
                    // The SDK sends getExclusiveStartKey(pageIndex+1) as nextToken for "current"
                    // re-fetches. That token is "B:{anchor(N+1)}:{cursor(N+1)}" from the adjacent
                    // page. cursor(N+1) = LEK of page N+1 = ExclusiveStartKey for page N, so
                    // ParseBackwardCursor gives the correct startKey for this page.
                    startKey    = ParseBackwardCursor(dataMeta.nextToken);
                    scanForward = false;
                }
                else
                {
                    // Forward page: previousToken is "F:{base64key}" (reliably echoed by SDK).
                    (scanForward, startKey) = ParseDirectionalToken(dataMeta.previousToken);
                }
            }
            else
            {
                startKey    = ResolveStartKey(dataMeta);
                // "first" must always scan forward — the SDK sends a stale oppositeDirection=true
                // when the user clicks "first" after a "last" navigation because React batches
                // the setIsOppositePaginationDirection(false) state update after fetchTableData runs.
                scanForward = dataMeta.paginationDirection == "first" || dataMeta.oppositeDirection != true;
                queryLimit  = dataMeta.rowsPerPage;
            }

            queryRequest.ExclusiveStartKey = startKey;
            queryRequest.ScanIndexForward  = scanForward;
            queryRequest.Limit             = queryLimit;

            var response = await dynamoClient.QueryAsync(queryRequest);

            foreach (var item in response.Items)
                result.Add(mapRow(item));

            StoreTokens(dataMeta, startKey, scanForward, response, requestContext, qId, queryLimit);
        }
        else
        {
            QueryResponse response;
            do
            {
                response = await dynamoClient.QueryAsync(queryRequest);
                foreach (var item in response.Items)
                    result.Add(mapRow(item));
                queryRequest.ExclusiveStartKey = response.LastEvaluatedKey;
            }
            while (response.LastEvaluatedKey?.Count > 0);
        }

        return result;
    }

    private static async Task<int> SetTotalRowCount(IAmazonDynamoDB dynamoClient, QueryRequest queryRequest, RequestContext requestContext)
    {
        var countRequest = new QueryRequest
        {
            TableName                 = queryRequest.TableName,
            IndexName                 = queryRequest.IndexName,
            KeyConditionExpression    = queryRequest.KeyConditionExpression,
            ExpressionAttributeNames  = queryRequest.ExpressionAttributeNames,
            ExpressionAttributeValues = queryRequest.ExpressionAttributeValues,
            FilterExpression          = queryRequest.FilterExpression,
            Select                    = Select.COUNT,
            ConsistentRead            = string.IsNullOrEmpty(queryRequest.IndexName)
        };

        int count = 0;
        Dictionary<string, AttributeValue>? lastKey = null;
        do
        {
            countRequest.ExclusiveStartKey = lastKey;
            try
            {
                var r = await dynamoClient.QueryAsync(countRequest);
                count  += r.Count;
                lastKey = r.LastEvaluatedKey;
            }
            catch { break; }
        }
        while (lastKey?.Count > 0);

        var meta = requestContext.GetDataTableMeta() ?? new Dictionary<string, object>();
        meta["rowCount"] = count;
        requestContext.SetDataTableMeta(meta);
        return count;
    }

    private static Dictionary<string, AttributeValue>? ResolveStartKey(DataTableMeta dataMeta)
    {
        return dataMeta.paginationDirection switch
        {
            "first" => null,
            "next"  when !string.IsNullOrEmpty(dataMeta.nextToken) => ParseNextKey(dataMeta.nextToken!),
            // Reversed "previous": cursor is embedded in nextToken as "B:{anchor}:{cursor}".
            "previous" when dataMeta.oppositeDirection == true && !string.IsNullOrEmpty(dataMeta.nextToken)
                => ParseBackwardCursor(dataMeta.nextToken!),
            // Standard forward "previous": previousToken carries the "F:{key}" prefix; strip it.
            "previous" when !string.IsNullOrEmpty(dataMeta.previousToken)
                => ExtractKey(dataMeta.previousToken!),
            "previous" when !string.IsNullOrEmpty(dataMeta.nextToken)
                => ParseNextKey(dataMeta.nextToken!),
            // "last" always scans backward from the table end; limit formula picks the page size.
            "last" => null,
            _      => null
        };
    }

    private static void StoreTokens(
        DataTableMeta dataMeta,
        Dictionary<string, AttributeValue>? startKey,
        bool scanForward,
        QueryResponse response,
        RequestContext? requestContext,
        string qId,
        int queryLimit)
    {
        if (requestContext == null) return;

        var meta = requestContext.GetDataTableMeta() ?? new Dictionary<string, object>();
        meta["qId"]               = qId;
        meta["oppositeDirection"] = !scanForward;
        meta["lastRequestLimit"]  = queryLimit;

        if (dataMeta.paginationDirection == "current")
        {
            if (!scanForward)
            {
                // Backward "current": the SDK sends getExclusiveStartKey(pageIndex+1) as nextToken
                // (the adjacent page's token, not this page's). Echoing it back would corrupt the
                // SDK's nextToken state and storeTokenForPage, causing the next "previous" click to
                // land on the same page again. Re-compute the correct backward token instead.
                string anchorPart = startKey?.Count > 0 ? Serialize(startKey) : "";
                string cursorPart = response.LastEvaluatedKey?.Count > 0 ? Serialize(response.LastEvaluatedKey) : "";
                meta["nextToken"] = $"B:{anchorPart}:{cursorPart}";
            }
            else
            {
                // Forward "current": the SDK sends the real current nextToken and previousToken,
                // so echo them back unchanged to support repeated modal open/close.
                if (!string.IsNullOrEmpty(dataMeta.nextToken))     meta["nextToken"]     = dataMeta.nextToken!;
                if (!string.IsNullOrEmpty(dataMeta.previousToken)) meta["previousToken"] = dataMeta.previousToken!;
            }
        }
        else if (!scanForward)
        {
            // Backward page: embed both the anchor (startKey of THIS page) and the next-backward
            // cursor (LastEvaluatedKey) into nextToken as "B:{anchor}:{cursor}".
            // "current" extracts the anchor to re-fetch this page; "previous" extracts the cursor
            // to continue scanning backward.
            string anchorPart = startKey?.Count > 0 ? Serialize(startKey) : "";
            string cursorPart = response.LastEvaluatedKey?.Count > 0 ? Serialize(response.LastEvaluatedKey) : "";
            meta["nextToken"]     = $"B:{anchorPart}:{cursorPart}";
            meta["previousToken"] = ""; // clear so the SDK doesn't echo a stale forward token
        }
        else
        {
            // Forward page.
            if (response.LastEvaluatedKey?.Count > 0)
                meta["nextToken"] = Serialize(response.LastEvaluatedKey);

            // previousToken = "F:{base64Key}" — prefix lets "current" recover the scan direction
            // and ExclusiveStartKey to re-fetch this page without relying on oppositeDirection.
            meta["previousToken"] = "F:" + (startKey?.Count > 0 ? Serialize(startKey) : "");
        }

        requestContext.SetDataTableMeta(meta);
    }

    // --- backward token helpers ---

    // Parses "B:{anchor}:{cursor}" and returns the cursor part:
    // the ExclusiveStartKey used to start the NEXT backward page — which is also the
    // ExclusiveStartKey needed to re-fetch the current page from the SDK's perspective
    // (the SDK sends the adjacent page's token for "current" re-fetches).
    // Falls back to plain base64 for legacy tokens that predate this format.
    private static Dictionary<string, AttributeValue>? ParseBackwardCursor(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        if (!token.StartsWith("B:", StringComparison.Ordinal))
            return Deserialize(token);
        string rest = token.Substring(2);
        int sep = rest.IndexOf(':');
        if (sep < 0) return null;
        string cursorPart = rest.Substring(sep + 1);
        return !string.IsNullOrEmpty(cursorPart) ? Deserialize(cursorPart) : null;
    }

    // Resolves nextToken for "next" direction, handling both forward (plain base64)
    // and backward ("B:{anchor}:{cursor}") token formats.
    private static Dictionary<string, AttributeValue>? ParseNextKey(string token)
    {
        if (token.StartsWith("B:", StringComparison.Ordinal))
            return ParseBackwardCursor(token);
        return Deserialize(token);
    }

    // --- direction-aware forward token helpers ---

    // Parses a "F:{base64}" / "R:{base64}" token into (scanForward, startKey).
    // Also handles plain base64 (no prefix) — the SDK sends getExclusiveStartKey(pageIndex-1)
    // which is the raw nextToken from the adjacent page, not our prefixed previousToken.
    private static (bool forward, Dictionary<string, AttributeValue>? key) ParseDirectionalToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return (true, null);
        bool hasPrefix = token.StartsWith("F:", StringComparison.Ordinal) || token.StartsWith("R:", StringComparison.Ordinal);
        bool forward   = !token.StartsWith("R:", StringComparison.Ordinal);
        string keyPart = hasPrefix ? (token.Length > 2 ? token.Substring(2) : string.Empty) : token;
        return (forward, !string.IsNullOrEmpty(keyPart) ? Deserialize(keyPart) : null);
    }

    // Strips the "F:"/"R:" direction prefix before deserialising (used for "previous" with previousToken).
    private static Dictionary<string, AttributeValue>? ExtractKey(string token)
    {
        if ((token.StartsWith("F:", StringComparison.Ordinal) ||
             token.StartsWith("R:", StringComparison.Ordinal)) && token.Length > 2)
            return Deserialize(token.Substring(2));
        // Legacy bare base64 (no prefix) — handle gracefully.
        if (token.Length > 2)
            return Deserialize(token);
        return null;
    }

    // --- serialisation helpers ---

    private static Dictionary<string, AttributeValue>? Deserialize(string base64Token)
        => JsonSerializer.Deserialize<Dictionary<string, AttributeValue>>(Convert.FromBase64String(base64Token));

    private static string Serialize(Dictionary<string, AttributeValue> key)
        => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(key, _tokenSerializerOptions));
}
