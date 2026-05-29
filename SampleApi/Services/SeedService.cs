using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace SampleApi.Services;

/// <summary>
/// Runs once at startup. If the FormRecords table has no EMPL records yet,
/// seeds it with 1 500 randomised employee rows so the app is demo-ready immediately.
/// </summary>
public class SeedService : IHostedService
{
    private const string PluginCode = "INTRA";
    private const string FormCode   = "EMPL";
    private const int    SeedCount  = 1_500;
    private const int    BatchSize  = 25;

    private readonly IAmazonDynamoDB _dynamo;
    private readonly string          _tableName;
    private readonly ILogger<SeedService> _log;

    public SeedService(IAmazonDynamoDB dynamo, IConfiguration config, ILogger<SeedService> log)
    {
        _dynamo    = dynamo;
        _tableName = config["AWS:DynamoDB:FormRecordsTable"] ?? "FormRecords";
        _log       = log;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        try
        {
            await EnsureTableExistsAsync(ct);

            if (await HasExistingRecords(ct))
            {
                _log.LogInformation("SeedService: EMPL records already exist — skipping seed.");
                return;
            }

            _log.LogInformation("SeedService: No EMPL records found — seeding {Count} records…", SeedCount);
            await SeedEmployees(ct);
            _log.LogInformation("SeedService: Seeding complete.");
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "SeedService: Seed failed (non-fatal — app will continue).");
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task EnsureTableExistsAsync(CancellationToken ct)
    {
        try
        {
            await _dynamo.DescribeTableAsync(new DescribeTableRequest { TableName = _tableName }, ct);
        }
        catch (ResourceNotFoundException)
        {
            _log.LogInformation("SeedService: Table {Table} not found — creating…", _tableName);
            await _dynamo.CreateTableAsync(new CreateTableRequest
            {
                TableName            = _tableName,
                BillingMode          = BillingMode.PAY_PER_REQUEST,
                KeySchema            = new List<KeySchemaElement>
                {
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE),
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S),
                },
            }, ct);

            string status;
            do
            {
                await Task.Delay(500, ct);
                var desc = await _dynamo.DescribeTableAsync(_tableName, ct);
                status = desc.Table.TableStatus;
            }
            while (status != "ACTIVE");

            _log.LogInformation("SeedService: Table {Table} is ready.", _tableName);
        }
    }

    private async Task<bool> HasExistingRecords(CancellationToken ct)
    {
        var response = await _dynamo.QueryAsync(new QueryRequest
        {
            TableName                 = _tableName,
            KeyConditionExpression    = "PK = :pk",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                { ":pk", new AttributeValue { S = $"{PluginCode}#{FormCode}" } }
            },
            Select  = Select.COUNT,
            Limit   = 1
        }, ct);

        return response.Count > 0;
    }

    private async Task SeedEmployees(CancellationToken ct)
    {
        var rng  = new Random();
        string now = DateTime.UtcNow.ToString("O");

        var items = Enumerable.Range(0, SeedCount)
            .Select(_ => BuildItem(rng, now))
            .ToList();

        for (int offset = 0; offset < items.Count; offset += BatchSize)
        {
            var batch = items
                .Skip(offset)
                .Take(BatchSize)
                .Select(item => new WriteRequest { PutRequest = new PutRequest { Item = item } })
                .ToList();

            await _dynamo.BatchWriteItemAsync(new BatchWriteItemRequest
            {
                RequestItems = new Dictionary<string, List<WriteRequest>>
                {
                    { _tableName, batch }
                }
            }, ct);
        }
    }

    private static Dictionary<string, AttributeValue> BuildItem(Random rng, string now)
    {
        string fn   = FirstNames[rng.Next(FirstNames.Length)];
        string ln   = LastNames[rng.Next(LastNames.Length)];
        string job  = Jobs[rng.Next(Jobs.Length)];
        string dept = Departments[rng.Next(Departments.Length)];
        string stat = Statuses[rng.Next(Statuses.Length)];
        string st   = Streets[rng.Next(Streets.Length)];
        string ci   = Cities[rng.Next(Cities.Length)];
        string dom  = Domains[rng.Next(Domains.Length)];

        int num  = rng.Next(1, 150);
        int zip  = rng.Next(10001, 99999);

        string emailLocal = $"{Ascii(fn)}.{Ascii(ln)}{rng.Next(1, 999)}";
        string email      = $"{emailLocal}@{dom}";
        string phone      = $"+372 5{rng.Next(100_000, 999_999)}";

        var startDate = RandomDate(rng, 2015, 2024);
        var endDate   = startDate.AddYears(rng.Next(1, 6));
        var dob       = RandomDate(rng, 1965, 2000);

        return new Dictionary<string, AttributeValue>
        {
            ["PK"]          = S($"{PluginCode}#{FormCode}"),
            ["SK"]          = S(Guid.NewGuid().ToString()),
            ["pluginCode"]  = S(PluginCode),
            ["formCode"]    = S(FormCode),
            ["firstname"]   = S(fn),
            ["lastname"]    = S(ln),
            ["email"]       = S(email),
            ["phonenumber"] = S(phone),
            ["address"]     = S($"{st} {num}, {ci}, Eesti, {zip}"),
            ["jobtitle"]    = S(job),
            ["department"]  = S(dept),
            ["status"]      = S(stat),
            ["startdate"]   = S(startDate.ToString("yyyy-MM-ddT09:00:00.000Z")),
            ["enddate"]     = S(endDate.ToString("yyyy-MM-ddT17:00:00.000Z")),
            ["dob"]         = S(dob.ToString("yyyy-MM-ddT00:00:00.000Z")),
            ["created_at"]  = S(now),
            ["updated_at"]  = S(now),
            ["updatedAt"]   = S(now),
        };
    }

    private static AttributeValue S(string value) => new() { S = value };

    private static DateTime RandomDate(Random rng, int fromYear, int toYear)
    {
        var from = new DateTime(fromYear, 1, 1);
        var to   = new DateTime(toYear,   12, 31);
        return from.AddDays(rng.Next((to - from).Days));
    }

    private static string Ascii(string s) =>
        s.ToLowerInvariant()
         .Replace('ü', 'u').Replace('õ', 'o').Replace('ö', 'o').Replace('ä', 'a');

    // ── Reference data ────────────────────────────────────────────────────────

    private static readonly string[] FirstNames =
    [
        "Maris","Toomas","Kristiina","Andres","Liis","Jüri","Kadri","Mikk","Piret","Rauno",
        "Siim","Triinu","Urmas","Moonika","Lauri","Kertu","Taavi","Reelika","Priit","Annika",
        "Märt","Epp","Veiko","Külli","Rait","Maike","Tanel","Heleri","Kaido","Tiina",
        "Risto","Kristi","Indrek","Sandra","Erki","Aive","Silver","Marko","Tiiu","Janek",
        "Merike","Tarmo","Triin","Jevgeni","Natalia","Ivan","Olga","Dmitri","Anna","Peeter"
    ];

    private static readonly string[] LastNames =
    [
        "Tamm","Kask","Männik","Lepik","Saar","Nurm","Org","Kukk","Kallas","Laine",
        "Mägi","Raudsepp","Pärn","Ilves","Vahter","Rebane","Hunt","Metsa","Sepp","Põder",
        "Oja","Rand","Kuusk","Liiv","Valk","Kivi","Mets","Roots","Paju","Hallik",
        "Leppik","Roos","Tikk","Harm","Aru","Kaur","Nõmm","Luik","Lepp","Käär",
        "Veski","Pold","Hanson","Bergmann","Koger","Pihl","Laan","Teder","Gross","Klein"
    ];

    private static readonly string[] Jobs =
    [
        "Projektijuht","Raamatupidaja","Müügijuht","Arendaja","Laojuhataja","Analüütik",
        "Disainer","Personalijuht","Logistik","Turundaja","Finantsjuht","Testija",
        "Süsteemiadministraator","Jurist","Sekretär","Klienditeenindaja","Insener",
        "Arhitekt","Juht","Koordinaator","Konsultant","Spetsialist","Assistent","Tehnik",
        "Müügiesindaja","Meister","Operaator","Kontrolör","Planeerija","Koolitaja"
    ];

    private static readonly string[] Departments =
    [
        "IT","Müük","Finants","Logistika","Personal","Turundus",
        "Tootmine","Haldus","Arendus","Klienditeenindus","Juhtimine",
        "Kvaliteet","Ostud","Õigus","Ladu"
    ];

    private static readonly string[] Statuses =
        ["Active","Active","Active","Active","Draft","In Progress","Completed","Cancelled"];

    private static readonly string[] Streets =
    [
        "Tartu mnt","Pärnu mnt","Narva mnt","Tallinna tn","Riia tn","Vabaduse pst",
        "Liivalaia tn","Gonsiori tn","Kaupmehe tn","Pikk tn","Lai tn","Müürivahe tn",
        "Nunne tn","Olevimägi","Viru tn","Estonia pst","Rävala pst","Lembitu tn"
    ];

    private static readonly string[] Cities =
    [
        "Tallinn","Tartu","Pärnu","Narva","Viljandi","Rakvere",
        "Kuressaare","Valga","Haapsalu","Jõhvi","Keila","Maardu",
        "Saue","Võru","Põlva"
    ];

    private static readonly string[] Domains =
        ["gmail.com","mail.ee","hot.ee","outlook.com","company.ee","work.ee","inbox.ee"];
}
