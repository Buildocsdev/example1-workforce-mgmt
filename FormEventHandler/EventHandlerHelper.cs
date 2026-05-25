using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;
using System.Runtime.ExceptionServices;

namespace FormEventHandler;

/// <summary>
/// Dispatches form lifecycle events to the correct handler method via reflection.
/// </summary>
public class EventHandlerHelper
{
    private readonly Type _handlerType;
    private readonly object _handlerInstance;
    private readonly HandlerContext _handlerContext;
    private string _handlerResponseMessage = string.Empty;

    public EventHandlerHelper(Type handlerType, object handlerInstance)
    {
        _handlerType = handlerType;
        _handlerInstance = handlerInstance ?? throw new ArgumentNullException(nameof(handlerInstance));
        _handlerContext = new HandlerContext();
    }

    /// <summary>
    /// Populates the handler context from the incoming request DTO.
    /// </summary>
    public async Task SetContext(PluginContextEventRequestDto context)
    {
        _handlerContext.SetContext(context);
        await InvokeMethod("Form_SetContextHandler", _handlerContext.GetHandlerContext());
    }

    public async Task RunFormInit(bool loadFromDb)
        => await InvokeMethod("Form_onInit", "onInit", string.Empty, loadFromDb);

    public async Task RunFieldOnChangeEvent(string widgetEvent, string widgetName, string? widgetValue)
        => await InvokeMethod($"{widgetName}_{widgetEvent}", widgetEvent, widgetName, widgetValue);

    /// <summary>
    /// Runs a direct event whose method name is specified in the event payload.
    /// </summary>
    public async Task<object?> RunOnDirectEvent(string widgetName, string? eventData)
    {
        if (string.IsNullOrEmpty(eventData)) return null;

        DirectEventRequestDto? eventRequestDto = JsonConvert.DeserializeObject<DirectEventRequestDto>(eventData);
        if (eventRequestDto == null) return null;

        return await InvokeMethodAndReturnResult($"{widgetName}_{eventRequestDto.MethodName}");
    }

    public async Task RunOnGetCustomResponse(string widgetEvent, string widgetName, string? eventData)
    {
        if (string.IsNullOrEmpty(eventData)) return;

        DirectEventRequestDto? eventRequestDto = JsonConvert.DeserializeObject<DirectEventRequestDto>(eventData);
        if (eventRequestDto == null) return;

        await InvokeMethod($"{widgetName}_{eventRequestDto.MethodName}", widgetEvent, widgetName, eventRequestDto.Value);
    }

    public async Task RunFormRefreshEvent(string widgetName, string? widgetValue)
    {
        await InvokeMethod($"{widgetName}_onRefresh", "onRefresh", widgetName, widgetValue);
        await InvokeMethod("Form_onRefresh", "onRefresh", widgetName, widgetValue);
    }

    public async Task RunWidgetOnClickEvent(string widgetEvent, string widgetName)
    {
        await InvokeMethod($"Form_{widgetEvent}", widgetEvent, widgetName, string.Empty);
        await InvokeMethod($"{widgetName}_{widgetEvent}", widgetEvent, widgetName, string.Empty);
        await InvokeMethod("Form_afterOnClick", widgetEvent, widgetName, string.Empty);
    }

    public async Task RunOnTableCreateRecordEvent(string widgetEvent, string widgetName)
    {
        await InvokeMethod($"{widgetName}_{widgetEvent}", widgetEvent, widgetName, string.Empty);
        await InvokeMethod($"Form_{widgetEvent}", widgetEvent, widgetName, string.Empty);
    }

    public async Task RunWidgetOnRunActionEvent(string widgetEvent, string widgetName, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        DataTableActionRequestDto? request = JsonConvert.DeserializeObject<DataTableActionRequestDto>(value);
        if (request?.Action != null && request?.RowData != null)
        {
            await InvokeTableActionMethod($"Form_{widgetEvent}", request.Action, request.RowData);
            await InvokeTableActionMethod($"{widgetName}_{widgetEvent}", request.Action, request.RowData);
        }
    }

    public async Task RunWidgetOnRunTableEditActionEvent(string widgetEvent, string widgetName, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        DataTableActionEditRequestDto? request = JsonConvert.DeserializeObject<DataTableActionEditRequestDto>(value);
        if (!string.IsNullOrEmpty(request?.Data))
        {
            await InvokeTableActionEditMethod($"Form_{widgetEvent}", request.Action, request.Data);
            await InvokeTableActionEditMethod($"{widgetName}_{widgetEvent}", request.Action, request.Data);
        }
    }

    public async Task RunWidgetOnRunBatchActionEvent(string widgetEvent, string widgetName, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        DataTableBatchActionRequestDto? request = JsonConvert.DeserializeObject<DataTableBatchActionRequestDto>(value);
        if (request?.Action != null && request?.Data != null)
        {
            await InvokeTableBatchActionMethod($"Form_{widgetEvent}", request.Action, request.Data);
            await InvokeTableBatchActionMethod($"{widgetName}_{widgetEvent}", request.Action, request.Data);
        }
    }

    public async Task<object?> RunWidgetOnTableLoadDataEvent(string widgetEvent, string widgetName)
    {
        object? result = await InvokeMethodAndReturnResult($"{widgetName}_{widgetEvent}");

        if (result?.GetType().GetProperties().Length == 0)
            result = await InvokeMethodAndReturnResult($"Form_{widgetEvent}");

        return result;
    }

    public async Task<object?> RunWidgetOnTableLoadRecordTemplatesEvent(string widgetEvent, string widgetName)
    {
        object? result = await InvokeMethodAndReturnResult($"{widgetName}_{widgetEvent}");

        if (result?.GetType().GetProperties().Length == 0)
            result = await InvokeMethodAndReturnResult($"Form_{widgetEvent}");

        return result;
    }

    public async Task<object?> RunWidgetOnTableLoadRelatedDataEvent(string widgetName)
        => await InvokeMethodAndReturnResult($"{widgetName}_onTableLoadRelatedData");

    public async Task<object?> RunWidgetOnTableLoadUserDataEvent()
        => await InvokeMethodAndReturnResult("Form_onTableLoadUserData");

    public async Task RunFormOnCancelEvent(string widgetEvent, string widgetName)
        => await InvokeMethod("Form_onCancel", widgetEvent, widgetName, string.Empty);

    public async Task RunFormOnRowCheckboxEvent(string widgetValue)
        => await InvokeMethod("Form_onRowCheckbox", "onRowCheckbox", string.Empty, widgetValue);

    public async Task RunFormOnPrintEvent(string widgetEvent, string widgetName)
        => await InvokeMethod("Form_onPrint", widgetEvent, widgetName, string.Empty);

    public async Task<(byte[] contents, string docName)> RunFormOnPrintEventAndReturnContents()
        => await InvokeMethodAndReturnContents("Form_onPrintReturnContents", "onPrintReturnContents", string.Empty, string.Empty);

    public async Task RunFormOnDownloadEvent(string widgetEvent, string widgetName)
        => await InvokeMethod("Form_onDownload", widgetEvent, widgetName, string.Empty);

    public async Task RunFormOnBeforeSaveEvent(string? widgetEvent = null, string? widgetName = null)
    {
        await InvokeMethod("Form_onBeforeSave", widgetEvent, widgetName, string.Empty);

        if (!string.IsNullOrEmpty(_handlerResponseMessage))
            throw new Exception(_handlerResponseMessage);
    }

    public async Task RunFormOnSaveEvent(string widgetEvent, string widgetName)
    {
        await RunFormOnBeforeSaveEvent(widgetEvent, widgetName);
        await InvokeMethod("Form_onSave", widgetEvent, widgetName, string.Empty);

        if (!string.IsNullOrEmpty(_handlerResponseMessage))
            throw new Exception(_handlerResponseMessage);

        await RunFormOnAfterSaveEvent(widgetEvent, widgetName);
    }

    public async Task RunFormOnAfterSaveEvent(string? widgetEvent = null, string? widgetName = null)
    {
        await InvokeMethod("Form_onAfterSave", widgetEvent, widgetName, string.Empty);

        if (!string.IsNullOrEmpty(_handlerResponseMessage))
            throw new Exception(_handlerResponseMessage);
    }

    public async Task RunFormOnBeforeSignEvent()
        => await InvokeMethod("Form_onBeforeSign", string.Empty, string.Empty, string.Empty);

    public async Task RunStartSignProcessEvent(string widgetEvent, string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        SignProcRequestDto? request = JsonConvert.DeserializeObject<SignProcRequestDto>(value);
        if (!string.IsNullOrEmpty(request?.Nidn))
            await InvokeSignProcessAsync($"Form_{widgetEvent}", request);
    }

    public async Task RunFinishSignProcessEvent(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        SignProcRequestDto? request = JsonConvert.DeserializeObject<SignProcRequestDto>(value);
        if (!string.IsNullOrEmpty(request?.Mode))
            await InvokeSignProcessAsync("Form_onFinishSignProcess", request);
    }

    public async Task RunFormOnDeleteEvent()
    {
        await InvokeMethod("Form_onBeforeDelete", string.Empty, string.Empty, string.Empty);
        if (!string.IsNullOrEmpty(_handlerResponseMessage)) throw new Exception(_handlerResponseMessage);

        await InvokeMethod("Form_onDelete", string.Empty, string.Empty, string.Empty);
        if (!string.IsNullOrEmpty(_handlerResponseMessage)) throw new Exception(_handlerResponseMessage);

        await InvokeMethod("Form_onAfterDelete", string.Empty, string.Empty, string.Empty);
        if (!string.IsNullOrEmpty(_handlerResponseMessage)) throw new Exception(_handlerResponseMessage);
    }

    public async Task RunFormBeforeFileUploadEvent(string widgetEvent, string widgetName, IFormFileCollection files)
    {
        await InvokeMethod("Form_onBeforeUpload", widgetEvent, widgetName, files);

        if (!string.IsNullOrEmpty(_handlerResponseMessage))
            throw new Exception(_handlerResponseMessage);
    }

    public async Task RunTableFileUploadEvent(string widgetEvent, string widgetName, IFormFileCollection files, string? uploadMode)
    {
        await InvokeMethodForFileUpload("Form_onTableUpload", widgetEvent, widgetName, uploadMode, files);

        if (!string.IsNullOrEmpty(_handlerResponseMessage))
            throw new Exception(_handlerResponseMessage);
    }

    public async Task RunFormAfterFileUploadEvent(string widgetEvent, string widgetName, List<string>? newFileList)
    {
        await InvokeMethod("Form_onAfterUpload", widgetEvent, widgetName, newFileList);

        if (!string.IsNullOrEmpty(_handlerResponseMessage))
            throw new Exception(_handlerResponseMessage);
    }

    public async Task<object?> GetFormData() => await InvokeMethod("GetData");
    public async Task<object?> GetRecord() => await InvokeMethod("GetRecord");
    public async Task<object?> GetRecordGuid() => await InvokeMethod("GetRecordGuid");
    public async Task<object?> GetFieldAllowedValues() => await InvokeMethod("GetFieldAllowedValues");
    public async Task<object?> GetRecordLayout() => await InvokeMethod("GetRecordLayout");
    public async Task<object?> GetCommands() => await InvokeMethod("GetCommands");

    public async Task<Screen?> GetScreen()
        => await InvokeMethod("GetScreen") as Screen;

    #region Private Methods

    private async Task InvokeMethod(string methodName, string? widgetEvent, string? widgetName, object? widgetValue)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return;

        Type[] methodParamsType = GetMethodParamTypes(_handlerType, methodName);
        object[] methodParams = GetMethodArgs(widgetEvent ?? string.Empty, methodParamsType, widgetName ?? string.Empty, widgetValue);

        MethodInfo? targetMethod = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, methodParamsType, null);
        await Invoke(targetMethod, methodParams);
    }

    private async Task<(byte[] contents, string docName)> InvokeMethodAndReturnContents(string methodName, string widgetEvent, string widgetName, object? widgetValue)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return (Array.Empty<byte>(), string.Empty);

        Type[] methodParamsType = GetMethodParamTypes(_handlerType, methodName);
        object[] methodParams = GetMethodArgs(widgetEvent, methodParamsType, widgetName, widgetValue);

        MethodInfo? targetMethod = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, methodParamsType, null);
        return await InvokeAndReturnFileContents(targetMethod, methodParams);
    }

    private async Task InvokeMethodForFileUpload(string methodName, string widgetEvent, string widgetName, string? uploadMode, object? widgetValue)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return;

        Type[] methodParamsType = GetMethodParamTypes(_handlerType, methodName);
        object[] methodParams = new object[] { widgetName!, widgetValue!, uploadMode! };

        MethodInfo? targetMethod = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, methodParamsType, null);
        await Invoke(targetMethod, methodParams);
    }

    private async Task InvokeTableActionEditMethod(string methodName, string? action, string data)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return;

        Type[] methodParamsType = GetMethodParamTypes(_handlerType, methodName);
        object[] methodParams = GetMethodArgs(methodParamsType, action ?? string.Empty, data);

        MethodInfo? targetMethod = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, methodParamsType, null);
        await Invoke(targetMethod, methodParams);
    }

    private async Task InvokeTableActionMethod(string methodName, string action, object data)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return;

        Type[] methodParamsType = GetMethodParamTypes(_handlerType, methodName);
        object[] methodParams = GetMethodArgs(methodParamsType, action, data);

        MethodInfo? targetMethod = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, methodParamsType, null);
        await Invoke(targetMethod, methodParams);
    }

    private async Task InvokeTableBatchActionMethod(string methodName, string action, object[] data)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return;

        Type[] methodParamsType = GetMethodParamTypes(_handlerType, methodName);
        object[] methodParams = GetMethodArgs(methodParamsType, action, data);

        MethodInfo? targetMethod = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, methodParamsType, null);
        await Invoke(targetMethod, methodParams);
    }

    private async Task InvokeSignProcessAsync(string methodName, SignProcRequestDto data)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return;

        Type[] methodParamsType = GetMethodParamTypes(_handlerType, methodName);
        MethodInfo? targetMethod = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, methodParamsType, null);
        await Invoke(targetMethod, new object[] { data });
    }

    public async Task<object?> InvokeMethod(string methodName, object[]? methodParams = null)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return null;

        object? targetObject = handlerMethodInfo.IsStatic ? null : _handlerInstance;
        methodParams ??= Array.Empty<object>();

        try
        {
            object? invocationResult = handlerMethodInfo.Invoke(targetObject, methodParams);

            if (invocationResult is Task task)
            {
                await task;

                if (task.IsFaulted && task.Exception != null)
                {
                    ExceptionDispatchInfo.Capture(task.Exception.InnerException ?? task.Exception).Throw();
                }

                if (task.GetType().IsGenericType)
                    return await ((dynamic)task).ConfigureAwait(false);

                return null;
            }

            return invocationResult;
        }
        catch (TargetInvocationException tie)
        {
            throw tie.InnerException ?? tie;
        }
    }

    private async Task InvokeMethod(string methodName, HandlerContext objectValue)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return;

        Type[] methodParamsType = GetMethodParamTypes(_handlerType, methodName);
        MethodInfo? targetMethod = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, methodParamsType, null);
        try
        {
            dynamic? awaitable = targetMethod?.Invoke(_handlerInstance, new object[] { objectValue });
            if (awaitable != null) await awaitable;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Unhandled exception in SetContextHandler: " + ex.Message);
        }
    }

    private async Task Invoke(MethodInfo? targetMethod, object[] methodParams)
    {
        Task? awaitable = null;
        try
        {
            if (targetMethod != null)
            {
                awaitable = (Task?)targetMethod.Invoke(_handlerInstance, methodParams);
                if (awaitable != null) await awaitable;
            }
        }
        catch
        {
            if (awaitable != null && RunBaseExceptionHandler(awaitable))
                throw;
        }
    }

    private async Task<(byte[] contents, string docName)> InvokeAndReturnFileContents(MethodInfo? targetMethod, object[] methodParams)
    {
        Task? awaitable = null;
        try
        {
            if (targetMethod != null)
            {
                awaitable = (Task?)targetMethod.Invoke(_handlerInstance, methodParams);
                if (awaitable != null)
                {
                    await awaitable.ConfigureAwait(false);

                    var resultProperty = awaitable.GetType().GetProperty("Result");
                    if (resultProperty != null)
                        return (ValueTuple<byte[], string>)resultProperty.GetValue(awaitable);
                }
            }
        }
        catch
        {
            if (awaitable != null && RunBaseExceptionHandler(awaitable))
                throw;
        }

        return (Array.Empty<byte>(), string.Empty);
    }

    public async Task<object?> InvokeMethodAndReturnResult(string methodName)
    {
        MethodInfo? handlerMethodInfo = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (handlerMethodInfo == null) return new object();

        Type[] methodParamsType = GetMethodParamTypes(_handlerType, methodName);
        MethodInfo? targetMethod = _handlerType.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, methodParamsType, null);
        try
        {
            dynamic? awaitable = targetMethod?.Invoke(_handlerInstance, Array.Empty<object>());
            if (awaitable != null) return await awaitable;
        }
        catch (Exception)
        {
            // Method not found or invocation failed — caller checks for null/empty result
        }

        return new object();
    }

    private object[] GetMethodArgs(string eventName, Type[] methodParamsType, string widgetName, object? widgetValue)
    {
        if (methodParamsType.Length == 0 || widgetValue == null)
            return Array.Empty<object>();

        string upperEvent = eventName.ToUpper();

        try
        {
            string[]? incomingValue = JArray.Parse((string)widgetValue).ToObject<string[]>();
            if (incomingValue != null)
            {
                if (upperEvent == EventHandlerEvents.OnRefresh)
                    return new object[] { widgetName, incomingValue };
                if (upperEvent == EventHandlerEvents.OnChange)
                    return new object[] { incomingValue };
            }
        }
        catch (Exception)
        {
            // Value is not a JSON array — treat as a scalar
            if (upperEvent == EventHandlerEvents.OnClick)
                return new object[] { widgetName };
            if (upperEvent == EventHandlerEvents.OnRefresh)
                return new object[] { widgetName, widgetValue! };
            if (upperEvent == EventHandlerEvents.OnChange || upperEvent == EventHandlerEvents.OnGetCustomResponse)
                return new object[] { widgetValue! };
            if (upperEvent == EventHandlerEvents.OnRowCheckbox)
                return new object[] { widgetValue! };
            if (upperEvent == EventHandlerEvents.OnInit)
                return new object[] { widgetValue! };
            if (upperEvent == EventHandlerEvents.OnTableRunActionEvent)
                return new object[] { widgetName, widgetValue! };
            if (upperEvent == EventHandlerEvents.OnFileUpload || upperEvent == EventHandlerEvents.OnFileUploadCreateDocumentEvent)
                return new object[] { widgetName, widgetValue! };
            if (upperEvent == EventHandlerEvents.OnPrintReturnContents)
                return new object[] { widgetValue! };
        }

        return Array.Empty<object>();
    }

    private object[] GetMethodArgs(Type[] methodParamsType, string action, object data)
        => methodParamsType.Length > 0 ? new object[] { action, data } : Array.Empty<object>();

    private static Type[] GetMethodParamTypes(Type t, string methodName)
    {
        MethodInfo? invoke = t.GetMethod(methodName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        ParameterInfo[]? existingParams = invoke?.GetParameters();

        if (existingParams == null || existingParams.Length == 0)
            return Array.Empty<Type>();

        var newParams = Array.Empty<Type>();
        foreach (ParameterInfo p in existingParams)
        {
            Type? paramType = (p.IsOut || p.ParameterType.IsByRef) ? p.ParameterType.GetElementType() : p.ParameterType;
            if (paramType != null) newParams = newParams.Concat(new Type[] { paramType }).ToArray();
        }
        return newParams;
    }

    private bool RunBaseExceptionHandler(Task awaitable)
    {
        AggregateException? allExceptions = awaitable.Exception;
        if (allExceptions == null) return false;

        foreach (var ex in allExceptions.InnerExceptions)
        {
            Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            return true;
        }

        return false;
    }

    #endregion
}
