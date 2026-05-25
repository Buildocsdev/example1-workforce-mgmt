namespace FormEventHandler;

public class Screen
{
    private Dictionary<string, bool> _visibility = new();
    private Dictionary<string, bool> _readonly = new();

    public void SetVisibility(string widgetName, bool visible)
        => _visibility[widgetName] = visible;

    public void SetReadonly(string widgetName, bool readOnly)
        => _readonly[widgetName] = readOnly;

    public void SetWidgetsVisibility(Dictionary<string, bool>? vis)
    {
        if (vis == null) return;
        foreach (var kv in vis) _visibility[kv.Key] = kv.Value;
    }

    public void SetWidgetsReadonly(Dictionary<string, bool>? ro)
    {
        if (ro == null) return;
        foreach (var kv in ro) _readonly[kv.Key] = kv.Value;
    }

    public bool HasModifiedState() => _visibility.Count > 0 || _readonly.Count > 0;
    public Dictionary<string, bool> GetChangedWidgetsVisibility() => _visibility;
    public Dictionary<string, bool> GetChangedWidgetsReadonly() => _readonly;

    // Stubs that satisfy the SDK contract. Override in a Screen subclass if you
    // need the request, localizer, or plugin helper to drive dynamic form changes.
    public void SetRequest(PluginContextEventRequestDto request) { }
    public void SetStringLocalizer(object? localizer) { }
    public void SetPluginHelper(PluginHelper pluginHelper) { }

    public Task<Dictionary<string, bool>> GetWidgetsFinalVisibility(string pluginCode, string formCode)
        => Task.FromResult(new Dictionary<string, bool>());

    public Task<Dictionary<string, bool>> GetWidgetsFinalReadonly(string pluginCode, string formCode)
        => Task.FromResult(new Dictionary<string, bool>());

    public Task<object?> GetUpdatedFormDefinition() => Task.FromResult<object?>(null);
}
