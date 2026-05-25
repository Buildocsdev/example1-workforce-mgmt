namespace FormEventHandler;

public class Cmd
{
    private readonly Dictionary<string, object> _cmd = new();
    private readonly Dictionary<string, Dictionary<string, string>> _fieldAllowedValues = new();
    private readonly List<UserMessageDto> _messages = new();

    private RequestContext? _requestContext;
    public void SetRequestContext(RequestContext ctx) => _requestContext = ctx;
    public RequestContext? RequestContext => _requestContext;

    // ── Messages ───────────────────────────────────────────────────────────────

    public void WarningMessage(string message) => SetMessage(MessageTypes.Warning, message);
    public void SuccessMessage(string message) => SetMessage(MessageTypes.Success, message);
    public void NotificationMessage(string message) => SetMessage(MessageTypes.Notice, message);

    public void SetMessage(string code, string text)
    {
        _messages.Add(new UserMessageDto { Code = code, Text = text });
        Set("Messages", _messages);
    }

    // ── Navigation ─────────────────────────────────────────────────────────────

    public void ShowRecord(string pluginCode, string formCode, string? guid, bool? isFormEditorMode = false, Dictionary<string, object>? prefillData = null)
    {
        OriginatorDto? originator = _requestContext?.GetOriginator();
        OriginatorDto? parent = _requestContext?.GetParent();
        Set("OpenModal", new { pluginCode, formCode, guid, isFormEditorMode, originator, parent, prefillData });
    }

    public void SetRedirectUrl(string toUrl)
        => Set("Redirect", new { url = toUrl });

    public void SetAfterRecordSave(object? value)
    {
        if (value != null)
            Set("AfterRecordSave", new { guid = value.ToString() });
        else
            _cmd.Remove("AfterRecordSave");
        SetRedrawScreen(value != null);
    }

    public void MoveToTab(string widgetName)
        => Set("MoveToTab", new { widgetName });

    // ── UI Components ──────────────────────────────────────────────────────────

    public void DisplayComponent(string componentName, object? options = null)
        => Set("DisplayComponent", new { componentName, options });

    public void CloseDisplayComponent(string componentName, object? options = null)
        => Set("CloseDisplayComponent", new { componentName, options });

    public void SetDisplayErrorPage(string message)
        => Set("DisplayErrorPage", new { message });

    public void SetPreviewDocument(string fileId, string? data, string? fieldName = null, object? context = null)
    {
        var noBase64 = new HashSet<string> { ".dwg", ".ifc", ".rvt", ".rfa", ".nwd", ".nwc", ".nwf" };
        data = noBase64.Contains(Path.GetExtension(fileId)) ? null : data;
        Set("PreviewDocument", new { context, fileId, data, fieldName, format = "pdf" });
    }

    public void SetRedrawScreen(bool redraw = true)
        => Set("RedrawScreen", redraw);

    public void SetHotReloadEnabled(object? context)
        => Set("HotReloadEnabled", context);

    // ── Data / State ───────────────────────────────────────────────────────────

    public void Event(string eventName)
        => Set(eventName, true);

    public void SetFormValidationData(Dictionary<string, string> requiredFields)
    {
        _cmd.Clear();
        Set("FormValidationData", requiredFields);
    }

    public void SetReplaceContextGuid(object? value)
        => Set("ReplaceContextRecordGuid", new { guid = value?.ToString() });

    public void SetRedrawFolders(string folderId, string isRoot = "")
        => Set("RedrawFolders", new { folderId, isRoot });

    public void SetCustomResponse(object? responseObj)
        => Set("CustomResponse", responseObj);

    public void FetchUserMenu(string projectGuid)
        => Set("FetchUserMenu", new { projectGuid });

    public void SetLoginData(string email, string password, string authHash, string url)
        => Set("Login", new { email, password, url, authHash });

    public void Download(string outFileName, string filePath)
        => Set("Download", new DownloadFileDetailsDto { OutFileName = outFileName, FilePath = filePath });

    public void PopulateSelectBoxList(string fieldName, Dictionary<string, string> values)
        => _fieldAllowedValues[fieldName] = values;

    // ── Output ─────────────────────────────────────────────────────────────────

    public Dictionary<string, object> GetCommands() => _cmd;
    public Dictionary<string, Dictionary<string, string>> GetFieldAvailableValues() => _fieldAllowedValues;

    public void Clear() => _cmd.Clear();
    public void ClearMessageBuffer() => _messages.Clear();

    private void Set(string key, object? value) => _cmd[key] = value!;
}
