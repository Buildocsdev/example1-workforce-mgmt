namespace FormEventHandler;

public class RequestContext
{
    private OriginatorDto? _originator;
    private OriginatorDto? _parent;
    private ParentOriginatorDto? _parentOriginator;
    private FolderDataDto? _folderData;
    private string? _prefillData;
    private string? _databaseTable;
    private string? _databaseFields;
    private PluginContextEventRequestDto? _requestData;
    private Dictionary<string, string> _requiredFields = new();
    private bool _isSystemForm;
    private bool _skipHandler;
    private bool _doNotSave;
    private Dictionary<string, object> _dataTableMeta = new();
    private Dictionary<string, string> _dataTableDefinitions = new();
    private Formatter _formatter = new();
    private FECommandProvider _cmd = new();

    // ── Setters ────────────────────────────────────────────────────────────────

    public void SetOriginator(OriginatorDto originator) => _originator = originator;
    public void SetParent(OriginatorDto parent) => _parent = parent;
    public void SetParentOriginator(ParentOriginatorDto parentOriginator) => _parentOriginator = parentOriginator;
    public void SetFolderData(FolderDataDto folderData) => _folderData = folderData;
    public void SetPrefillData(string data) => _prefillData = data;
    public void SetDatabaseTable(string dbTableName) => _databaseTable = dbTableName;
    public void SetDatabaseFields(string fieldNames) => _databaseFields = fieldNames;
    public void SetRequestData(PluginContextEventRequestDto requestData) => _requestData = requestData;
    public void SetRequiredFields(Dictionary<string, string> requiredFields) => _requiredFields = requiredFields;
    public void SetSystemForm(bool isSystemForm) => _isSystemForm = isSystemForm;
    public void SetSkipHandler(bool skip) => _skipHandler = skip;
    public void SetDoNotSave(bool doNotSave) => _doNotSave = doNotSave;
    public void SetFECommandProvider(FECommandProvider feCommandProvider) => _cmd = feCommandProvider;
    public void SetDataTableMeta(Dictionary<string, object> datatableDefinitions) => _dataTableMeta = datatableDefinitions;
    public void SetDataTableDefinitions(Dictionary<string, string> datatableDefinitions) => _dataTableDefinitions = datatableDefinitions;
    public void SetFormatter(Formatter formatter) => _formatter = formatter;

    // ── Getters ────────────────────────────────────────────────────────────────

    // Stub — returns null in this boilerplate. Wire up to your auth system
    // (e.g. IHttpContextAccessor + JWT claims) to surface the current user.
    public ICurrentUser? GetCurrentUser() => null;
    public OriginatorDto? GetOriginator() => _originator;
    public OriginatorDto? GetParent() => _parent;
    public ParentOriginatorDto? GetParentOriginator() => _parentOriginator;
    public FolderDataDto? GetFolderData() => _folderData;
    public string? GetPrefillData() => _prefillData;
    public string? GetDatabaseTable() => _databaseTable;
    public string? GetDatabaseFields() => _databaseFields;
    public PluginContextEventRequestDto? GetRequestData() => _requestData;
    public Dictionary<string, string> GetRequiredFields() => _requiredFields;
    public bool GetSystemForm() => _isSystemForm;
    public bool GetSkipHandler() => _skipHandler;
    public bool GetDoNotSave() => _doNotSave;
    public FECommandProvider GetFECommandProvider() => _cmd;
    public Dictionary<string, object> GetDataTableMeta() => _dataTableMeta;
    public string? GetDatatableDefinition(string? tblName = null)
        => tblName != null && _dataTableDefinitions.TryGetValue(tblName, out var def) ? def : null;
    public Formatter GetFormatter() => _formatter;
}
