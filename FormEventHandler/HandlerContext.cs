using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace FormEventHandler;

/// <summary>
/// Context class for widget data (matches the widgetContext JSON sent by the frontend).
/// </summary>
public class Context
{
    [JsonProperty("originator")]
    public OriginatorDto? Originator { get; set; }

    [JsonProperty("parent")]
    public OriginatorDto? Parent { get; set; }

    [JsonProperty("parentOriginator")]
    public ParentOriginatorDto? ParentOriginator { get; set; }

    [JsonProperty("folderData")]
    public FolderDataDto? FolderData { get; set; }

    [JsonProperty("prefillData")]
    public string? PrefillData { get; set; }

    [JsonProperty("hasParentForm")]
    public bool HasParentForm { get; set; }

    [JsonProperty("isModal")]
    public bool IsModal { get; set; }
}

/// <summary>
/// Prefill data converter for JSON deserialization.
/// </summary>
public class PrefillDataStringConverter : JsonConverter<string>
{
    public override string? ReadJson(JsonReader reader, Type objectType, string? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;
        if (reader.TokenType == JsonToken.String)
            return (string?)reader.Value;
        // Non-string value (object, array, number, bool): consume all tokens and
        // return as a compact JSON string so the parent reader advances correctly.
        return JToken.Load(reader).ToString(Formatting.None);
    }

    public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
    {
        writer.WriteValue(value);
    }
}

/// <summary>
/// Handler context for storing and managing event handler context data.
/// </summary>
public class HandlerContext
{
    private readonly Dictionary<string, object> _context = new();
    private Context? _internalContext = null;

    public HandlerContext Set(string key, object? value)
    {
        _context[key] = value!;
        return this;
    }

    public object? Get(string key)
        => _context.TryGetValue(key, out var value) ? value : null;

    public Dictionary<string, object>? GetContext()
    {
        return _context;
    }

    public HandlerContext GetHandlerContext()
    {
        return this;
    }

    internal HandlerContext SetContext(PluginContextEventRequestDto? request)
    {
        if (request == null) throw new Exception("The event handler request context is missing.");

        Set("pluginCode", request.PluginCode);
        Set("formCode", request.FormCode);
        Set("guid", request.Guid);
        Set("formData", request.FormData);
        Set("projectGuid", request.ProjectGuid);

        if (request?.WidgetContext != null)
        {
            var settings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter> { new PrefillDataStringConverter() }
            };
            _internalContext = JsonConvert.DeserializeObject<Context>(request.WidgetContext, settings);

            if (_internalContext != null)
            {
                Set("originator", _internalContext.Originator);
                Set("parent", _internalContext.Parent);
                Set("parentOriginator", _internalContext.ParentOriginator);
                Set("folderData", _internalContext.FolderData);
                Set("hasParentForm", _internalContext.HasParentForm);
                Set("isModal", _internalContext.IsModal);

                // Top-level prefillData is present on form-load requests.
                // On widget events (onChange, onSave, etc.) the SDK nests it at originator.params.prefillData.
                // Resolve whichever path is populated so handlers see a consistent "prefillData" key.
                if (!string.IsNullOrEmpty(_internalContext.PrefillData))
                {
                    Set("prefillData", _internalContext.PrefillData);
                }
                else
                {
                    try
                    {
                        var raw = JObject.Parse(request.WidgetContext);
                        var nested = raw["originator"]?["params"]?["prefillData"];
                        if (nested != null && nested.Type != JTokenType.Null)
                        {
                            string prefillStr = nested.Type == JTokenType.String
                                ? nested.Value<string>()!
                                : nested.ToString(Formatting.None);
                            if (!string.IsNullOrEmpty(prefillStr))
                                Set("prefillData", prefillStr);
                        }
                    }
                    catch (Exception) { } // prefillData absent or malformed — treat as no prefill
                }
            }
        }

        return this;
    }
}