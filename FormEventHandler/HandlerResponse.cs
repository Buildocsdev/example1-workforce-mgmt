namespace FormEventHandler;

public class HandlerResponse
{
    public const string FECommand          = "FECommand";
    public const string FormData           = "data";
    public const string FormDefinition     = "definition";
    public const string FieldAllowedValues = "fieldAllowedValues";
    public const string WidgetData         = "widgetData";
    public const string WidgetRelatedData  = "widgetRelatedData";
    public const string WidgetsState       = "widgetsState";
    public const string TableMeta          = "tableMeta";
    public const string UserData           = "userData";
    public const string Layout             = "layout";
    public const string FolderData         = "folderData";
    public const string RequiredFields     = "requiredFields";
    public const string LinkedFiles             = "linkedFiles";
    public const string LinkedFileDeleteSuccess = "linkedFileDeleteSuccess";
    public const string UiTranslations          = "uiTranslations";

    private readonly Dictionary<string, object?> _data = new();

    public void Set(string key, object? value) => _data[key] = value;

    public Dictionary<string, object?> Get() => _data;
}
