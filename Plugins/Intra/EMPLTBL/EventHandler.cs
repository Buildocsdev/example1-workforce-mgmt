using Amazon.DynamoDBv2.Model;
using FormEventHandler;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Intra.EMPLTBL;

public class EventHandler : GenericFormHandler
{
    public EventHandler() { }
    public EventHandler(HandlerContext context) : base(context) { }

    public virtual async Task employees_onTableCreateRecordEvent()
    {
        await SaveCurrentAndUpdateOriginator();

        string? pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString();
        if (string.IsNullOrEmpty(pluginCode)) return;

        cmd.ShowRecord(pluginCode, "EMPL", null);
    }

    public async Task<List<Dictionary<string, string>>> employees_onTableLoadData()
    {
        if (_dynamoClient == null || _dbTableName == null) return new List<Dictionary<string, string>>();

        string pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString() ?? "";
        var systemKeys = new HashSet<string> { "PK", "SK", "updatedAt" };

        var queryRequest = new QueryRequest
        {
            TableName = _dbTableName,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"{pluginCode}#EMPL" } }
            }
        };

        return await TableQueryHelper.ExecutePagedQueryAsync(_dynamoClient, queryRequest, item =>
        {
            var row = new Dictionary<string, string>();
            if (item.TryGetValue("PK", out var pk)) row["_pk"] = pk.S;
            if (item.TryGetValue("SK", out var sk)) row["_sk"] = sk.S;
            foreach (var kv in item)
                if (!systemKeys.Contains(kv.Key) && kv.Value.S != null)
                    row[kv.Key] = string.Equals(kv.Key, "status", StringComparison.OrdinalIgnoreCase) ? Translate(kv.Value.S) : kv.Value.S;
            row["_plugincode"] = row["pluginCode"];
            row["_code"] = row["formCode"];
            return row;
        }, _requestContext, qId: "employees");
    }

    public async Task exportbtn_OnClick()
    {
        string pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString() ?? "";

        var queryRequest = new QueryRequest
        {
            TableName = _dbTableName,
            KeyConditionExpression = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"{pluginCode}#EMPL" } }
            }
        };

        var allItems = new List<Dictionary<string, AttributeValue>>();
        QueryResponse queryResponse;
        do
        {
            queryResponse = await _dynamoClient!.QueryAsync(queryRequest);
            allItems.AddRange(queryResponse.Items);
            queryRequest.ExclusiveStartKey = queryResponse.LastEvaluatedKey;
        }
        while (queryResponse.LastEvaluatedKey?.Count > 0);

        string[] headers = { "First Name", "Last Name", "Email", "Phone", "Address", "Job Title", "Department", "Status", "Start Date", "End Date", "Date of Birth" };
        string[] fields  = { "firstname",   "lastname",  "email", "phonenumber", "address", "jobtitle", "department", "status", "startdate", "enddate", "dob" };
        
        IWorkbook workbook = new XSSFWorkbook();
        ISheet sheet = workbook.CreateSheet("Employees");

        IFont boldFont = workbook.CreateFont();
        boldFont.IsBold = true;
        ICellStyle headerStyle = workbook.CreateCellStyle();
        headerStyle.SetFont(boldFont);

        IRow headerRow = sheet.CreateRow(0);
        for (int i = 0; i < headers.Length; i++)
        {
            ICell cell = headerRow.CreateCell(i);
            cell.SetCellValue(headers[i]);
            cell.CellStyle = headerStyle;
            sheet.SetColumnWidth(i, 22 * 256);
        }

        ICellStyle textStyle = workbook.CreateCellStyle();
        textStyle.DataFormat = workbook.CreateDataFormat().GetFormat("@");

        var dateFields = new HashSet<string> { "startdate", "enddate", "dob" };

        int rowNum = 1;
        foreach (var item in allItems)
        {
            IRow sheetRow = sheet.CreateRow(rowNum++);
            for (int i = 0; i < fields.Length; i++)
            {
                ICell cell = sheetRow.CreateCell(i);
                cell.CellStyle = textStyle;
                string raw = item.TryGetValue(fields[i], out var v) ? v.S ?? "" : "";
                if (dateFields.Contains(fields[i]) && DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    cell.SetCellValue(dt.ToString("dd.MM.yyyy"));
                else
                    cell.SetCellValue(raw);
            }
        }

        string tempFile = Path.Combine(Path.GetTempPath(), "employees_" + Path.GetRandomFileName() + ".xlsx");
        using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
            workbook.Write(fs, false);

        cmd.Download("Employee List.xlsx", tempFile);
    }

}
