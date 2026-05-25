using Newtonsoft.Json;

namespace FormEventHandler;

/// <summary>
/// Request DTO for plugin context events (load form, run event, file operations).
/// </summary>
public class PluginContextEventRequestDto
{
    [JsonProperty("pluginCode")]
    public string PluginCode { get; set; } = string.Empty;

    [JsonProperty("formCode")]
    public string FormCode { get; set; } = string.Empty;

    [JsonProperty("guid")]
    public string Guid { get; set; } = string.Empty;

    [JsonProperty("projectGuid")]
    public string ProjectGuid { get; set; } = string.Empty;

    [JsonProperty("widgetName")]
    public string? WidgetName { get; set; }

    [JsonProperty("widgetEvent")]
    public string? WidgetEvent { get; set; }

    [JsonProperty("widgetValue")]
    public string? WidgetValue { get; set; }

    [JsonProperty("widgetContext")]
    public string? WidgetContext { get; set; }

    [JsonProperty("formData")]
    public Dictionary<string, object>? FormData { get; set; }

    [JsonProperty("originator")]
    public OriginatorDto? Originator { get; set; }

    [JsonProperty("isFormEditor")]
    public bool? IsFormEditor { get; set; }

    [JsonProperty("previewMode")]
    public bool? PreviewMode { get; set; }

    [JsonProperty("widgetsState")]
    public WidgetsStateDto? WidgetsState { get; set; }

    [JsonProperty("definition")]
    public object? Definition { get; set; }

    [JsonProperty("fieldAllowedValues")]
    public Dictionary<string, object>? FieldAllowedValues { get; set; }

    [JsonProperty("requiredFields")]
    public Dictionary<string, string>? RequiredFields { get; set; }

    [JsonProperty("hasParentForm")]
    public bool? HasParentForm { get; set; }

    [JsonProperty("uiTranslations")]
    public Dictionary<string, string>? UiTranslations { get; set; }
}

public class WidgetsStateDto
{
    [JsonProperty("visibility")]
    public Dictionary<string, bool>? Visibility { get; set; }

    [JsonProperty("readOnly")]
    public Dictionary<string, bool>? Readonly { get; set; }
}

public class DirectEventRequestDto
{
    [JsonProperty("methodName")]
    public string MethodName { get; set; } = string.Empty;

    [JsonProperty("value")]
    public string? Value { get; set; }
}

public class DataTableActionRequestDto
{
    [JsonProperty("action")]
    public string? Action { get; set; }

    [JsonProperty("context")]
    public Context? Context { get; set; }

    [JsonProperty("rowData")]
    public RowData? RowData { get; set; }
}

/// <summary>
/// Row identifier fields prefixed with underscore to distinguish them from data columns.
/// These names mirror what the frontend table widget sends in action requests.
/// </summary>
public class BatchRowData
{
    public string _sk { get; set; } = default!;
    public string _code { get; set; } = default!;
    public string? _origformcode { get; set; }
    public string _plugincode { get; set; } = default!;
    public string? _desc { get; set; }
}

public class RowData : BatchRowData
{
    public string _id { get; set; } = default!;
}

public class DataTableActionEditRequestDto
{
    [JsonProperty("action")]
    public string? Action { get; set; }

    [JsonProperty("data")]
    public string? Data { get; set; }
}

public class DataTableBatchActionRequestDto
{
    [JsonProperty("action")]
    public string? Action { get; set; }

    [JsonProperty("data")]
    public object[]? Data { get; set; }
}

public class SignProcRequestDto
{
    [JsonProperty("nidn")]
    public string? Nidn { get; set; }

    [JsonProperty("mode")]
    public string? Mode { get; set; }
}

public class UserMessageDto
{
    public string? Code { get; set; }
    public string? Text { get; set; }
}

public class DownloadFileDetailsDto
{
    [JsonProperty("outFileName")]
    public string? OutFileName { get; set; }

    [JsonProperty("filePath")]
    public string? FilePath { get; set; }
}

public class RecordRequestDto
{
    [JsonProperty("pluginCode")]
    public string PluginCode { get; set; } = string.Empty;

    [JsonProperty("formCode")]
    public string FormCode { get; set; } = string.Empty;

    [JsonProperty("guid")]
    public string? Guid { get; set; }

    [JsonProperty("pGuid")]
    public string? PGuid { get; set; }
}

public class OriginatorDto
{
    [JsonProperty("params")]
    public RecordRequestDto? Params { get; set; }

    [JsonProperty("widgetName")]
    public string? WidgetName { get; set; }
}

public class ParentOriginatorDto
{
    [JsonProperty("params")]
    public RecordRequestDto Params { get; set; } = new();
}

public class FolderDataDto
{
    [JsonProperty("folderId")]
    public string? FolderId { get; set; }

    [JsonProperty("folderName")]
    public string? FolderName { get; set; }
}
