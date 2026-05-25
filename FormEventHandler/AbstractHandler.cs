using Amazon.DynamoDBv2;

namespace FormEventHandler;

public abstract class AbstractHandler
{
    protected Cmd cmd = new();
    protected DataRecord record = new();
    protected Screen screen = new();
    protected HandlerContext contextHandlerInstance = new();

    protected IAmazonDynamoDB? _dynamoClient;
    protected string? _dbTableName;
    protected RequestContext? _requestContext;
    private bool _doNotSave;

    private IFormLocalizer? _localizer;

    protected AbstractHandler() { }

    protected AbstractHandler(HandlerContext context)
    {
        contextHandlerInstance = context;
    }

    public void SetLocalizer(IFormLocalizer localizer) => _localizer = localizer;

    protected string Translate(string key, params object[] args)
    {
        string? pluginCode = contextHandlerInstance.Get("pluginCode")?.ToString();
        return _localizer?.Translate(key, pluginCode, args.Length > 0 ? args : null) ?? key;
    }

    // ── DB configuration ───────────────────────────────────────────────────────

    public void ConfigureDb(IAmazonDynamoDB client, string tableName)
    {
        _dynamoClient = client;
        _dbTableName = tableName;
    }

    public void SetRequestContext(RequestContext ctx)
    {
        _requestContext = ctx;
        cmd.SetRequestContext(ctx);
    }

    public void SetDoNotSave(bool doNotSave) => _doNotSave = doNotSave;

    // ── Context injection ──────────────────────────────────────────────────────

    public void Form_SetContextHandler(HandlerContext instance)
    {
        contextHandlerInstance = instance;

        if (_dynamoClient != null && _dbTableName != null)
        {
            string? pluginCode = instance.Get("pluginCode")?.ToString();
            string? formCode = instance.Get("formCode")?.ToString();
            string? guid = instance.Get("guid")?.ToString();
            record.Configure(_dynamoClient, _dbTableName, pluginCode, formCode, guid);
        }
    }

    // ── Lifecycle events ───────────────────────────────────────────────────────

    public virtual async Task Form_onInit(bool loadFromDb) { }
    public virtual async Task Form_onBeforeSave() { }
    public virtual async Task Form_onSave()
    {
        string now = DateTime.UtcNow.ToString("O");
        if (record.IsNewByRequest())
            record.SetField("created_at", now);
        record.SetField("updated_at", now);

        AssignNewRecordGuid();
        await record.Save();
        cmd.SetAfterRecordSave(record.GetRecordGuid());
    }
    public virtual async Task Form_onAfterSave() { }
    public virtual async Task Form_onBeforeDelete() { }
    public virtual async Task Form_onDelete() { await record.Delete(true); }
    public virtual async Task Form_onAfterDelete() { }
    public virtual async Task Form_onCancel() { }
    public virtual async Task Form_onConfirm() { }
    public virtual async Task Form_onClick(string fieldName) { }
    public virtual async Task Form_afterOnClick(string fieldName) { }
    public virtual async Task Form_onRefresh(string fieldName, object value) { }
    public virtual async Task Form_onPrint() { }
    public virtual async Task Form_onStartSignProcess(SignProcRequestDto request) { }
    public virtual async Task Form_onFinishSignProcess(SignProcRequestDto request) { }
    public virtual async Task Form_onBeforeUpload(string fieldName, object files)
    {
        bool wasNew = record.IsNewByRequest();
        AssignNewRecordGuid();
        if (wasNew)
            cmd.SetReplaceContextGuid(record.GetRecordGuid());
    }
    public virtual async Task Form_onAfterUpload(string fieldName, List<string>? uploadedFiles) { }
    public virtual async Task Form_onTableRunActionEvent(string action, RowData rowData) { }
    public virtual async Task Form_onTableRunBatchActionEvent(string action, object[] data) { }
    public virtual async Task Form_onTableEditRunActionEvent(string action, string data) { }

    public virtual async Task Form_onTableCreateRecordEvent()
    {
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void AssignNewRecordGuid()
    {
        if (!record.IsNewByRequest()) return;
        string? originatorGuid = _requestContext?.GetOriginator()?.Params?.Guid;
        string newGuid = Guid.NewGuid().ToString();
        record.SetRecordGuid(string.IsNullOrEmpty(originatorGuid) ? newGuid : $"{originatorGuid}#{newGuid}");
    }

    // ── Save helper ────────────────────────────────────────────────────────────

    // Mirrors Buildocs AbstractHandler.SaveCurrentAndUpdateOriginator.
    // Saves the current record if needed (new → always, existing → when clearCmd is true
    // and guid is not "index"), then updates the originator guid so child forms opened
    // afterward receive the correct parent guid.
    public async Task SaveCurrentAndUpdateOriginator(ParentOriginatorDto? parentOriginatorDto = null,
        bool clearCmd = true, bool doValidation = true)
    {
        if (_doNotSave) return;

        string? guid = contextHandlerInstance.Get("guid")?.ToString();

        if (record.IsNewByRequest())
        {
            OriginatorDto? originalOriginatorDto = _requestContext?.GetOriginator();
            OriginatorDto? originalParentDto = _requestContext?.GetParent();
            _requestContext?.SetOriginator(new OriginatorDto { Params = parentOriginatorDto?.Params });

            if (doValidation) await Form_onBeforeSave();
            await Form_onSave();

            _requestContext?.SetOriginator(originalOriginatorDto);
            _requestContext?.SetParent(originalParentDto);

            string? newGuid = record.GetRecordGuid();

            OriginatorDto? originatorDto = _requestContext?.GetOriginator();
            bool isNewGuid = string.Compare(originatorDto?.Params?.Guid, "NEW", StringComparison.OrdinalIgnoreCase) == 0;

            if (string.IsNullOrEmpty(originatorDto?.Params?.Guid) || isNewGuid)
                originatorDto!.Params!.Guid = newGuid;

            OriginatorDto? parentDto = _requestContext?.GetParent();
            if (string.IsNullOrEmpty(parentDto?.Params?.Guid) || isNewGuid)
                parentDto!.Params!.Guid = newGuid;

            if (clearCmd)
                cmd.SetReplaceContextGuid(newGuid);
        }
        else
        {
            bool isIndex = string.Equals(guid, "index", StringComparison.OrdinalIgnoreCase);
            if (clearCmd && !isIndex)
            {
                if (doValidation) await Form_onBeforeSave();
                await Form_onSave();
            }

            OriginatorDto? origDto = _requestContext?.GetOriginator();
            if (origDto?.WidgetName == null)
            {
                PluginContextEventRequestDto? pDto = _requestContext?.GetRequestData();
                _requestContext?.SetOriginator(new OriginatorDto
                {
                    WidgetName = pDto?.WidgetName,
                    Params = new RecordRequestDto
                    {
                        PluginCode = pDto?.PluginCode ?? string.Empty,
                        FormCode = pDto?.FormCode ?? string.Empty,
                        Guid = pDto?.Guid,
                        PGuid = pDto?.ProjectGuid
                    }
                });
            }

        }
    }

    // ── Data accessors (called by EventHandlerHelper via reflection) ───────────

    public virtual Dictionary<string, object> GetData() => record.GetData();
    public virtual Dictionary<string, object> GetCommands() => cmd.GetCommands();
    public virtual Dictionary<string, Dictionary<string, string>> GetFieldAllowedValues()
        => cmd.GetFieldAvailableValues();
    public virtual DataRecord GetRecord() => record;
    public virtual Screen GetScreen() => screen;
    public virtual string? GetRecordGuid() => record.GetRecordGuid();
}
