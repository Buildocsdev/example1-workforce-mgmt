using Amazon.DynamoDBv2;
using System.Reflection;
using Newtonsoft.Json;

namespace FormEventHandler;

public class PluginHelper
{
    private readonly PluginContextEventRequestDto _request;
    private readonly RequestContext _requestContext;
    private readonly IAmazonDynamoDB? _dynamoClient;
    private readonly string? _dbTableName;
    private readonly IFormLocalizer? _localizer;

    public PluginHelper(PluginContextEventRequestDto request, RequestContext requestContext,
        IAmazonDynamoDB? dynamoClient = null, string? dbTableName = null, IFormLocalizer? localizer = null)
    {
        _request        = request;
        _requestContext = requestContext;
        _dynamoClient   = dynamoClient;
        _dbTableName    = dbTableName;
        _localizer      = localizer;
    }

    // "INTRA" → "Intra", "form001" → "Form001" (matches Assembly.Load casing)
    private static string Normalize(string code)
        => string.IsNullOrEmpty(code)
            ? code
            : char.ToUpper(code[0]) + code.Substring(1).ToLower();

    // Resolves to: {PluginCode}.{FormCode}.EventHandler
    // Folder layout: Plugins/{PluginCode}/{FormCode}/EventHandler.cs
    public static string GetPluginEventHandlerNamespace(string pluginCode, string formCode)
        => $"{Normalize(pluginCode)}.{Normalize(formCode)}.EventHandler";

    // Loads the plugin assembly by normalized name and returns the EventHandler type.
    // Falls back to Type.GetType for types already loaded in the current AppDomain.
    public static async Task<Type?> GetHandlerType(string pluginCode, string formCode)
    {
        string assemblyName = Normalize(pluginCode);
        string typeName = GetPluginEventHandlerNamespace(pluginCode, formCode);
        try
        {
            Assembly assembly = Assembly.Load(assemblyName);
            return assembly.GetType(typeName, throwOnError: false, ignoreCase: true);
        }
        catch
        {
            return Type.GetType(typeName, throwOnError: false, ignoreCase: true);
        }
    }

    public virtual Task<object?> GetFormDefinition() => Task.FromResult<object?>(null);

    public virtual async Task<object?> GetHandlerInstance(Type? t, PluginContextEventRequestDto eventRequest)
    {
        object? objInstance = null;

        try
        {
            // Parse widgetContext and populate _requestContext — mirrors IPluginContextProvider.Set* calls
            // in the original GetHandlerInstance.
            if (eventRequest?.WidgetContext != null)
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = new List<JsonConverter> { new PrefillDataStringConverter() }
                };
                Context ctx = JsonConvert.DeserializeObject<Context>(eventRequest.WidgetContext, settings);

                if (ctx?.FolderData != null)      _requestContext.SetFolderData(ctx.FolderData);
                if (ctx?.Originator != null)      _requestContext.SetOriginator(ctx.Originator);
                if (ctx?.Parent != null)           _requestContext.SetParent(ctx.Parent);
                if (ctx?.ParentOriginator != null) _requestContext.SetParentOriginator(ctx.ParentOriginator);
                if (ctx?.PrefillData != null)      _requestContext.SetPrefillData(ctx.PrefillData);
            }

            _requestContext.SetRequestData(eventRequest);
            if (eventRequest?.RequiredFields != null)
                _requestContext.SetRequiredFields(eventRequest.RequiredFields);
            _requestContext.SetFormatter(new Formatter());

            // Build HandlerContext that is passed into the plugin handler constructor.
            HandlerContext handlerContext = new HandlerContext();
            handlerContext.SetContext(eventRequest);

            if (t != null)
            {
                try
                {
                    objInstance = Activator.CreateInstance(t, handlerContext);
                }
                catch (MissingMethodException)
                {
                    objInstance = Activator.CreateInstance(t);
                }

                if (_dynamoClient != null && _dbTableName != null && objInstance is AbstractHandler abstractHandler)
                {
                    abstractHandler.ConfigureDb(_dynamoClient, _dbTableName);
                    abstractHandler.SetRequestContext(_requestContext);
                    if (_localizer != null) abstractHandler.SetLocalizer(_localizer);
                }
            }
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException ?? ex;
            throw new InvalidOperationException(
                $"Unable to create handler instance for '{t?.FullName}': [{inner.GetType().Name}] {inner.Message}", ex);
        }

        return objInstance;
    }
}
