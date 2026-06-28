using Amazon.DynamoDBv2;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SampleApi.Services;
using FormEventHandler;
using FileStorage;
using Newtonsoft.Json;

namespace SampleApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DemoController : ControllerBase
{
    private readonly IAwsDemoService _awsDemoService;
    private readonly ILogger<DemoController> _logger;
    private readonly IFileStorageProvider _fileStorage;
    private readonly IConfiguration _configuration;
    private readonly IAmazonDynamoDB _dynamoClient;
    private readonly IFormLocalizer _localizer;
    private readonly string _formRecordsTable;

    private readonly RequestContext _requestContext = new();

    public DemoController(IAwsDemoService awsDemoService, ILogger<DemoController> logger,
        IFileStorageProvider fileStorage, IConfiguration configuration,
        IAmazonDynamoDB dynamoClient, IFormLocalizer localizer)
    {
        _awsDemoService = awsDemoService;
        _logger = logger;
        _fileStorage = fileStorage;
        _configuration = configuration;
        _dynamoClient = dynamoClient;
        _localizer = localizer;
        _formRecordsTable = configuration["AWS:DynamoDB:FormRecordsTable"] ?? "FormRecords";
    }

    private PluginHelper CreatePluginHelper(PluginContextEventRequestDto request)
        => new PluginHelper(request, _requestContext, _dynamoClient, _formRecordsTable, _localizer);

    [HttpPost("test")]
    public async Task<IActionResult> Test()
    {
        _logger.LogInformation("Test endpoint called");
        try
        {
            var (success, message) = await _awsDemoService.RunDemoAsync();
            _logger.LogInformation("After RunDemoAsync");
            return Ok(new { success, message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Test endpoint");
            return StatusCode(500, new { error = "An unexpected error occurred" });
        }
    }

    [HttpPost("loadform")]
    public async Task<IActionResult> LoadForm([FromBody] PluginContextEventRequestDto request)
    {
        HandlerResponse response = new HandlerResponse();
        response.Set(HandlerResponse.FormDefinition, request.Definition);
        response.Set(HandlerResponse.FieldAllowedValues, request.FieldAllowedValues);
        response.Set(HandlerResponse.FormData, request.FormData);
        response.Set(HandlerResponse.RequiredFields, request.RequiredFields);
        response.Set(HandlerResponse.WidgetsState, request.WidgetsState);
        response.Set(HandlerResponse.UiTranslations, request.UiTranslations);

        EventHandlerHelper? eventHandlerHelper = null;

        try
        {
            if (request?.Originator != null)
                _requestContext.SetOriginator(request.Originator);

            PluginHelper pluginHelper = CreatePluginHelper(request);
            Type? t = await PluginHelper.GetHandlerType(request.PluginCode, request.FormCode);

            object? objInstance = await pluginHelper.GetHandlerInstance(t, request);
            if (objInstance != null && t != null)
            {
                eventHandlerHelper = new EventHandlerHelper(t, objInstance);
                await eventHandlerHelper.SetContext(request);

                Screen? screen = await eventHandlerHelper.GetScreen();
                screen?.SetRequest(request);
                screen?.SetWidgetsVisibility(request.WidgetsState?.Visibility);
                screen?.SetWidgetsReadonly(request.WidgetsState?.Readonly);

                await eventHandlerHelper.RunFormInit(true);

                // Handler fieldAllowedValues overrides Buildocs base; fall back to request values.
                var handlerAllowedValues = await eventHandlerHelper.GetFieldAllowedValues() as Dictionary<string, Dictionary<string, string>>;
                if (handlerAllowedValues?.Count > 0)
                    response.Set(HandlerResponse.FieldAllowedValues, handlerAllowedValues);

                // Handler data merged onto Buildocs base; handler wins on conflict.
                var handlerData = await eventHandlerHelper.GetFormData() is Dictionary<string, object> hd ? hd : null;
                
                var mergedData = new Dictionary<string, object>(request.FormData ?? new());
                if (handlerData != null)
                    foreach (var kv in handlerData) mergedData[kv.Key] = kv.Value;
                response.Set(HandlerResponse.FormData, mergedData);

                response.Set(HandlerResponse.Layout, await eventHandlerHelper.GetRecordLayout());

                if (screen?.HasModifiedState() == true)
                {
                    screen.SetPluginHelper(pluginHelper);
                    response.Set(HandlerResponse.FormDefinition, await screen.GetUpdatedFormDefinition() ?? request.Definition);
                    response.Set(HandlerResponse.WidgetsState, new
                    {
                        visibility = screen.GetChangedWidgetsVisibility(),
                        readOnly = screen.GetChangedWidgetsReadonly()
                    });
                }
                else
                {
                    response.Set(HandlerResponse.FormDefinition, await pluginHelper.GetFormDefinition() ?? request.Definition);
                }
            }
        }
        catch (RecordNotFoundException ex)
        {
            _requestContext.GetFECommandProvider().SetMessage(MessageTypes.Warning, ex.Message);
            _requestContext.GetFECommandProvider().Event("CloseForm");
        }
        catch (Exception ex)
        {
            if (ex is not ActionTerminationException)
            {
                if (ex is not UserWarningException)
                {
                    if (eventHandlerHelper != null && await eventHandlerHelper.GetCommands() is Dictionary<string, object> cmds)
                    {
                        var msgs = cmds.TryGetValue("Messages", out var m) && m is List<UserMessageDto> existing
                            ? existing : new List<UserMessageDto>();
                        msgs.Add(new UserMessageDto { Code = "WARNING", Text = ex.Message });
                        cmds["Messages"] = msgs;
                    }
                }
                else
                {
                    _requestContext.GetFECommandProvider().SetMessage(MessageTypes.Warning, ex.Message);
                }
            }
        }

        var feCommands = eventHandlerHelper != null ? await eventHandlerHelper.GetCommands() : null;
        response.Set(HandlerResponse.FECommand, feCommands);

        return Ok(response.Get());
    }

    // POST api/demo/runevent
    [HttpPost("runevent")]
    public virtual async Task<IActionResult> RunEvent([FromBody] PluginContextEventRequestDto request)
    {
        var response = new HandlerResponse();
        response.Set(HandlerResponse.FieldAllowedValues, null);
        response.Set(HandlerResponse.FormData, null);
        response.Set(HandlerResponse.WidgetData, null);
        response.Set(HandlerResponse.WidgetsState, null);

        response.Set(HandlerResponse.FormDefinition, null);

        EventHandlerHelper? eventHandlerHelper = null;

        try
        {
            PluginHelper pluginHelper = CreatePluginHelper(request);
            Type? t = await PluginHelper.GetHandlerType(request.PluginCode, request.FormCode);
            object? objInstance = await pluginHelper.GetHandlerInstance(t, request);

            if (objInstance != null && t != null)
            {
                eventHandlerHelper = new EventHandlerHelper(t, objInstance);
                await eventHandlerHelper.SetContext(request);
                await eventHandlerHelper.RunFormInit(false);

                Screen? screen = await eventHandlerHelper.GetScreen() as Screen;
                if (screen != null)
                {
                    if (request.WidgetsState?.Visibility?.Count > 0)
                        screen.SetWidgetsVisibility(request.WidgetsState.Visibility);
                    if (request.WidgetsState?.Readonly?.Count > 0)
                        screen.SetWidgetsReadonly(request.WidgetsState.Readonly);
                }

                string? baseEvent = request.WidgetEvent?.ToUpper();
                string widgetEvent = request.WidgetEvent!;
                string widgetName = request.WidgetName!;

                try
                {
                    if (baseEvent == EventHandlerEvents.OnChange)
                    {
                        await eventHandlerHelper.RunFieldOnChangeEvent(widgetEvent, widgetName, request.WidgetValue);
                        await eventHandlerHelper.RunFormRefreshEvent(widgetName, request.WidgetValue);
                    }
                    else if (baseEvent == EventHandlerEvents.OnRefresh)
                    {
                        await eventHandlerHelper.RunFormRefreshEvent(widgetName, request.WidgetValue);
                    }
                    else if (baseEvent == EventHandlerEvents.OnClick)
                    {
                        await eventHandlerHelper.RunWidgetOnClickEvent(widgetEvent, widgetName);
                    }
                    else if (baseEvent == EventHandlerEvents.OnCancel)
                    {
                        await eventHandlerHelper.RunFormOnCancelEvent(widgetEvent, widgetName);
                    }
                    else if (baseEvent == EventHandlerEvents.OnDelete)
                    {
                        await eventHandlerHelper.RunFormOnDeleteEvent();
                    }
                    else if (baseEvent == EventHandlerEvents.OnPrint)
                    {
                        await eventHandlerHelper.RunFormOnPrintEvent(widgetEvent, widgetName);
                    }
                    else if (baseEvent == EventHandlerEvents.OnSave)
                    {
                        await eventHandlerHelper.RunFormOnSaveEvent(widgetEvent, widgetName);
                    }
                    else if (baseEvent == EventHandlerEvents.OnTableCreateRecordEvent)
                    {
                        await eventHandlerHelper.RunOnTableCreateRecordEvent(widgetEvent, widgetName);
                    }
                    else if (baseEvent == EventHandlerEvents.OnTableLoadData)
                    {
                        response.Set(HandlerResponse.WidgetData, await eventHandlerHelper.RunWidgetOnTableLoadDataEvent(widgetEvent, widgetName));
                        response.Set(HandlerResponse.WidgetRelatedData, await eventHandlerHelper.RunWidgetOnTableLoadRelatedDataEvent(widgetName));
                    }
                    else if (baseEvent == EventHandlerEvents.OnTableEditLoadData)
                    {
                        response.Set(HandlerResponse.WidgetData, await eventHandlerHelper.RunWidgetOnTableLoadDataEvent(widgetEvent, widgetName));
                        response.Set(HandlerResponse.WidgetRelatedData, await eventHandlerHelper.RunWidgetOnTableLoadRelatedDataEvent(widgetName));
                    }
                    else if (baseEvent == EventHandlerEvents.OnTableRunActionEvent)
                    {
                        await eventHandlerHelper.RunWidgetOnRunActionEvent(widgetEvent, widgetName, request.WidgetValue);
                        response.Set(HandlerResponse.WidgetRelatedData, await eventHandlerHelper.RunWidgetOnTableLoadRelatedDataEvent(widgetName));
                    }
                    else if (baseEvent == EventHandlerEvents.OnTableEditRunActionEvent)
                    {
                        await eventHandlerHelper.RunWidgetOnRunTableEditActionEvent(widgetEvent, widgetName, request.WidgetValue);
                        response.Set(HandlerResponse.WidgetRelatedData, await eventHandlerHelper.RunWidgetOnTableLoadRelatedDataEvent(widgetName));
                        response.Set(HandlerResponse.WidgetData, await eventHandlerHelper.RunWidgetOnTableLoadDataEvent(EventHandlerEvents.OnTableEditLoadData, widgetName));
                    }
                    else if (baseEvent == EventHandlerEvents.OnTableRunBatchActionEvent)
                    {
                        await eventHandlerHelper.RunWidgetOnRunBatchActionEvent(widgetEvent, widgetName, request.WidgetValue);
                        response.Set(HandlerResponse.WidgetData, await eventHandlerHelper.RunWidgetOnTableLoadDataEvent(EventHandlerEvents.OnTableLoadData, widgetName));
                    }
                    else if (baseEvent == EventHandlerEvents.OnDirectRunFormEvent)
                    {
                        await eventHandlerHelper.RunOnDirectEvent(widgetName, request.WidgetValue);
                    }
                    else if (baseEvent == EventHandlerEvents.OnGetCustomResponse)
                    {
                        await eventHandlerHelper.RunOnGetCustomResponse(widgetEvent, widgetName, request.WidgetValue);
                    }
                    else
                    {
                        throw new NotImplementedException($"Event {request.WidgetEvent} is not implemented.");
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not ActionTerminationException)
                    {
                        if (ex is not UserWarningException)
                            throw;
                        else
                        {
                            if (await eventHandlerHelper.GetCommands() is Dictionary<string, object> cmds)
                            {
                                var msgs = cmds.TryGetValue("Messages", out var m) && m is List<UserMessageDto> existing
                                    ? existing : new List<UserMessageDto>();
                                msgs.Add(new UserMessageDto { Code = "WARNING", Text = ex.Message });
                                cmds["Messages"] = msgs;
                            }
                        }
                    }
                }

                response.Set(HandlerResponse.FECommand, await eventHandlerHelper.GetCommands());
                response.Set(HandlerResponse.FieldAllowedValues, await eventHandlerHelper.GetFieldAllowedValues());
                response.Set(HandlerResponse.FormData, await eventHandlerHelper.GetFormData());
                response.Set(HandlerResponse.TableMeta, _requestContext.GetDataTableMeta());
                if (request.Definition != null)
                    response.Set(HandlerResponse.FormDefinition, request.Definition);

                _logger.LogDebug("RunEvent responded to {Event} — definition={DefinitionStatus}",
                    request.WidgetEvent,
                    response.Get().TryGetValue("definition", out var _rd) ? (_rd == null ? "null" : "present") : "absent");

                if (screen?.HasModifiedState() == true)
                {
                    response.Set(HandlerResponse.WidgetsState, new
                    {
                        visibility = screen.GetChangedWidgetsVisibility(),
                        readOnly = screen.GetChangedWidgetsReadonly()
                    });
                }
            }
        }
        catch (ActionTerminationException)
        {
            if (eventHandlerHelper != null)
                response.Set(HandlerResponse.FECommand, await eventHandlerHelper.GetCommands());
        }
        catch (Exception ex) when (ex is not UserWarningException)
        {
            _logger.LogError(ex, "RunEvent error for {FormCode}/{Event}", request?.FormCode, request?.WidgetEvent);
        }

        if (eventHandlerHelper != null)
        {
            var cmds = await eventHandlerHelper.GetCommands() as Dictionary<string, object>;
            if (cmds != null
                && cmds.TryGetValue("Download", out var dl)
                && dl is DownloadFileDetailsDto dd
                && !string.IsNullOrEmpty(dd.FilePath)
                && System.IO.File.Exists(dd.FilePath))
            {
                Response.ContentType = _fileStorage.GetMimeType(dd.OutFileName ?? "file.xlsx");
                Response.Headers.Append("Content-Disposition",
                    $"attachment; filename*=UTF-8''{Uri.EscapeDataString(dd.OutFileName ?? "download.xlsx")}");
                using (var fs = new FileStream(dd.FilePath!, FileMode.Open, FileAccess.Read))
                    await _fileStorage.WriteInChunksAsync(Response.Body, fs);
                System.IO.File.Delete(dd.FilePath!);
                return new EmptyResult();
            }
        }

        return Ok(response.Get());
    }

    // POST api/demo/uploadfiles
    [HttpPost("uploadfiles")]
    [Consumes("multipart/form-data")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<IActionResult> UploadFiles()
    {
        var response = new HandlerResponse();
        response.Set(HandlerResponse.FieldAllowedValues, null);
        response.Set(HandlerResponse.FormData, null);
        response.Set(HandlerResponse.WidgetData, null);

        string projectGuid = Request.Form["pGuid"].ToString();

        PluginContextEventRequestDto request = new PluginContextEventRequestDto
        {
            ProjectGuid = string.IsNullOrEmpty(projectGuid) ? string.Empty : projectGuid,
            PluginCode = Request.Form["pluginCode"].ToString(),
            FormCode = Request.Form["formCode"].ToString(),
            Guid = Request.Form["guid"].ToString(),
            WidgetName = Request.Form["WidgetName"].ToString(),
            WidgetEvent = Request.Form["WidgetEvent"].ToString(),
            WidgetContext = Request.Form["WidgetContext"].ToString(),
            FormData = !string.IsNullOrEmpty(Request.Form["formData"])
                        ? JsonConvert.DeserializeObject<Dictionary<string, object>>(Request.Form["formData"].ToString(), new JsonSerializerSettings
                        {
                            DateParseHandling = DateParseHandling.None
                        })
                        : new Dictionary<string, object>()
        };

        PluginHelper pluginHelper = CreatePluginHelper(request);
        Type? t = await PluginHelper.GetHandlerType(request.PluginCode, request.FormCode);

        object? objInstance = await pluginHelper.GetHandlerInstance(t, request);
        if (objInstance != null && t != null)
        {
            List<string>? linkedFiles = new();
            EventHandlerHelper eventHandlerHelper = new EventHandlerHelper(t, objInstance);
            await eventHandlerHelper.SetContext(request);
            await eventHandlerHelper.RunFormInit(false);

            string baseEvent = (request.WidgetEvent ?? string.Empty).ToUpper();

            try
            {
                if (baseEvent == EventHandlerEvents.OnFileUpload)
                {
                    await eventHandlerHelper.RunFormBeforeFileUploadEvent(request.WidgetEvent!, request.WidgetName!, Request.Form.Files);

                    DataRecord dataRecord = (DataRecord)(await eventHandlerHelper.GetRecord())!;

                    var result = await DoUploadFiles(dataRecord, request.FormData);
                    List<string>? newFileList = result.newFilesList;
                    linkedFiles = result.allFilesList;

                    await eventHandlerHelper.RunFormAfterFileUploadEvent(request.WidgetEvent!, request.WidgetName!, newFileList);
                }
                else if (baseEvent == EventHandlerEvents.OnFileUploadCreateDocumentEvent)
                {
                    string uploadMode = Request.Form["uploadMode"].ToString();
                    await eventHandlerHelper.RunTableFileUploadEvent(request.WidgetEvent!, request.WidgetName!, Request.Form.Files, uploadMode);
                }
            }
            catch (Exception ex)
            {
                if (ex is not ActionTerminationException)
                {
                    if (ex is not UserWarningException)
                        throw;
                    else
                    {
                        if (await eventHandlerHelper.GetCommands() is Dictionary<string, object> cmds)
                        {
                            var msgs = cmds.TryGetValue("Messages", out var m) && m is List<UserMessageDto> existing
                                ? existing : new List<UserMessageDto>();
                            msgs.Add(new UserMessageDto { Code = "WARNING", Text = ex.Message });
                            cmds["Messages"] = msgs;
                        }
                    }
                }
            }

            response.Set(HandlerResponse.FECommand, await eventHandlerHelper.GetCommands());
            response.Set(HandlerResponse.LinkedFiles, linkedFiles);
            response.Set(HandlerResponse.FieldAllowedValues, await eventHandlerHelper.GetFieldAllowedValues());
            response.Set(HandlerResponse.FormData, await eventHandlerHelper.GetFormData());
        }

        return Ok(response.Get());
    }

    private List<string> GetExistingLinkedFiles(DataRecord dataRecord, string fieldName)
    {
        string? exRecFileData = dataRecord.GetField(fieldName)?.ToString();
        return !string.IsNullOrEmpty(exRecFileData)
            ? JsonConvert.DeserializeObject<List<string>>(exRecFileData) ?? new List<string>()
            : new List<string>();
    }

    private async Task<(List<string>? newFilesList, List<string>? allFilesList)> DoUploadFiles(
        DataRecord dataRecord, Dictionary<string, object>? formData)
    {
        List<string> linkedFiles = new();
        List<string> newFiles = new();

        var files = Request.Form.Files;

        if (dataRecord == null)
            throw new Exception("Unable to upload files because dataRecord is undefined.");

        if (files == null || files.Count == 0)
            throw new Exception("No file data.");

        string fieldName = Request.Form["fieldName"].ToString();
        string guid = Request.Form["guid"].ToString();

        if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(guid))
            throw new Exception("Destination record parameters are missing.");

        dataRecord.SetData(formData);
        linkedFiles = GetExistingLinkedFiles(dataRecord, fieldName);

        string tenantId = _fileStorage.GetTenantId();
        string destUploadPath = _fileStorage.GetFileUploadPath(tenantId, "f", DateTime.Now);

        foreach (var file in files)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0;

            string filePath = $"{destUploadPath}/{file.FileName}";
            string linkedFile = await _fileStorage.UploadFileAsync(filePath, ms, file.ContentType);

            if (!linkedFiles.Contains(linkedFile))
            {
                linkedFiles.Add(linkedFile);
                newFiles.Add(linkedFile);
            }
        }

        dataRecord.LinkFiles(fieldName, linkedFiles);
        await dataRecord.Save();

        return (newFiles, linkedFiles);
    }

    // POST api/demo/deletefile
    [HttpPost("deletefile")]
    public async Task<IActionResult> DeleteFile([FromBody] FileRequestDto request)
    {
        bool result = false;
        HandlerResponse response = new HandlerResponse();

        if (!string.IsNullOrEmpty(request.FileId))
        {
            string[] parts = request.FileId.Split(':', 2);
            if (parts.Length == 2)
            {
                string bucketName = parts[0];
                string filePath = parts[1];

                PluginContextEventRequestDto requestDto = new PluginContextEventRequestDto
                {
                    PluginCode = request.PluginCode,
                    FormCode = request.FormCode,
                    Guid = request.Guid
                };

                PluginHelper pluginHelper = CreatePluginHelper(requestDto);
                Type? t = await PluginHelper.GetHandlerType(requestDto.PluginCode, requestDto.FormCode);
                object? objInstance = await pluginHelper.GetHandlerInstance(t, requestDto);

                if (objInstance != null && t != null)
                {
                    EventHandlerHelper eventHandlerHelper = new EventHandlerHelper(t, objInstance);
                    await eventHandlerHelper.SetContext(requestDto);
                    DataRecord dataRecord = (DataRecord)(await eventHandlerHelper.GetRecord())!;
                    await dataRecord.LoadRecord();

                    if (await _fileStorage.DeleteFileAsync(filePath, bucketName))
                    {
                        string? linkedFilesStrData = dataRecord.GetField(request.FieldName)?.ToString();
                        if (!string.IsNullOrEmpty(linkedFilesStrData))
                        {
                            List<string>? linkedFiles = JsonConvert.DeserializeObject<List<string>>(linkedFilesStrData);
                            linkedFiles?.Remove(request.FileId);
                            dataRecord.LinkFiles(request.FieldName, linkedFiles);
                            await dataRecord.Save();
                            response.Set(HandlerResponse.LinkedFiles, linkedFiles);
                        }
                        result = true;
                    }
                }
            }
        }

        response.Set(HandlerResponse.LinkedFileDeleteSuccess, result);
        return Ok(response.Get());
    }

    // POST api/demo/downloadfile
    [HttpPost("downloadfile")]
    public async Task<IActionResult> DownloadFile([FromBody] FileRequestDto request)
    {
        if (string.IsNullOrEmpty(request.FileId))
            return BadRequest("FileId is required");

        string[] parts = request.FileId.Split(':', 2);
        if (parts.Length != 2)
            return BadRequest("Invalid FileId format — expected 'bucket:path'");

        string bucketName = parts[0];
        string filePath = parts[1];
        string fileName = Path.GetFileName(filePath);

        Stream? fileStream = await _fileStorage.GetFileStreamAsync(filePath, bucketName);
        if (fileStream == null)
            return NotFound("File not found");

        Response.ContentType = _fileStorage.GetMimeType(fileName);
        Response.Headers.Append("Content-Disposition", $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileName)}");
        await _fileStorage.WriteInChunksAsync(Response.Body, fileStream);
        return new EmptyResult();
    }

    // POST api/demo/getpresignedurl
    [HttpPost("getpresignedurl")]
    public async Task<IActionResult> GetPresignedUrl([FromBody] LinkedFileRequestDto request)
    {
        if (string.IsNullOrEmpty(request.FileId))
            return BadRequest("FileId is required");

        string[] parts = request.FileId.Split(':', 2);
        if (parts.Length != 2)
            return BadRequest("Invalid FileId format — expected 'bucket:path'");

        string url = await _fileStorage.GetPresignedUrlAsync(parts[1], parts[0]);
        return Ok(new Dictionary<string, object?> { ["presignedUrl"] = url });
    }

    // POST api/demo/getlinkedfilebychunks
    // Streams a byte range (HTTP 206) for large files such as PDFs and videos.
    [HttpPost("getlinkedfilebychunks")]
    public async Task<IActionResult> GetLinkedFileByChunks([FromBody] LinkedFileRequestDto request)
    {
        if (string.IsNullOrEmpty(request.FileId))
            return BadRequest("FileId is required");

        string[] parts = request.FileId.Split(':', 2);
        if (parts.Length != 2)
            return BadRequest("Invalid FileId format — expected 'bucket:path'");

        string bucketName = parts[0];
        string filePath = parts[1];
        string fileName = Path.GetFileName(filePath);
        string mimeType = _fileStorage.GetMimeType(fileName);

        var meta = await _fileStorage.GetFileMetadataAsync(filePath, bucketName);
        if (meta == null) return NotFound("File not found.");
        long fileSize = meta.ContentLength;
        if (fileSize <= 0) return NotFound("File size unknown.");

        string rangeHeader = Request.Headers["Range"].ToString();
        if (string.IsNullOrEmpty(rangeHeader) || !rangeHeader.StartsWith("bytes="))
            return BadRequest("Range header is required (e.g. bytes=0-81919)");

        string[] rangeParts = rangeHeader.Replace("bytes=", "").Split('-');
        if (!long.TryParse(rangeParts[0], out long start))
            return StatusCode(416, "Invalid Range header");
        long end = rangeParts.Length > 1 && long.TryParse(rangeParts[1], out long parsedEnd)
            ? parsedEnd
            : fileSize - 1;

        if (start >= fileSize || end >= fileSize || start > end)
            return StatusCode(416, "Requested Range Not Satisfiable");

        long chunkSize = end - start + 1;

        using Stream? fileStream = await _fileStorage.GetFileStreamAsync(filePath, bucketName);
        if (fileStream == null) return NotFound("File not found.");

        // Skip to the requested start byte
        byte[] skipBuffer = new byte[81920];
        long bytesToSkip = start;
        while (bytesToSkip > 0)
        {
            int toRead = (int)Math.Min(skipBuffer.Length, bytesToSkip);
            int skipped = await fileStream.ReadAsync(skipBuffer, 0, toRead);
            if (skipped == 0) break;
            bytesToSkip -= skipped;
        }

        // Read the requested chunk
        using var chunkStream = new MemoryStream();
        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        while (totalRead < chunkSize
            && (bytesRead = await fileStream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, chunkSize - totalRead))) > 0)
        {
            await chunkStream.WriteAsync(buffer, 0, bytesRead);
            totalRead += bytesRead;
        }

        Response.StatusCode = 206;
        Response.Headers.Append("Accept-Ranges", "bytes");
        Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{fileSize}");

        return File(chunkStream.ToArray(), mimeType);
    }

    // POST api/demo/getlinkedfilemeta
    [HttpPost("getlinkedfilemeta")]
    public async Task<IActionResult> GetLinkedFileMeta([FromBody] LinkedFileRequestDto request)
    {
        if (string.IsNullOrEmpty(request.FileId))
            return BadRequest("FileId is required");

        string[] parts = request.FileId.Split(':', 2);
        if (parts.Length != 2)
            return BadRequest("Invalid FileId format — expected 'bucket:path'");

        string bucketName = parts[0];
        string filePath = parts[1];
        string fileName = filePath.Split('/').LastOrDefault() ?? string.Empty;

        var meta = await _fileStorage.GetFileMetadataAsync(filePath, bucketName);

        string mimeType = _fileStorage.GetMimeType(fileName);
        return Ok(new Dictionary<string, object?>
        {
            ["linkedFileName"] = fileName,
            ["linkedFileSize"] = meta?.ContentLength ?? 0,
            ["mimeType"] = mimeType,
            ["linkedFileMimeType"] = mimeType   // fileviewer component reads this key
        });
    }

    // POST api/demo/getlinkedfile
    [HttpPost("getlinkedfile")]
    public async Task<IActionResult> GetLinkedFile([FromBody] LinkedFileRequestDto request)
    {
        if (string.IsNullOrEmpty(request.FileId))
            return BadRequest("FileId is required");

        string[] parts = request.FileId.Split(':', 2);
        if (parts.Length != 2)
            return BadRequest("Invalid FileId format — expected 'bucket:path'");

        string bucketName = parts[0];
        string filePath = parts[1];
        string fileExt = Path.GetExtension(filePath).TrimStart('.');

        byte[]? fileData = await _fileStorage.GetFileBytesAsync(filePath, bucketName);

        return Ok(new Dictionary<string, object?>
        {
            ["linkedFile"] = fileData != null ? Convert.ToBase64String(fileData) : null,
            ["linkedFileExt"] = fileExt,
            ["linkedFileSize"] = fileData?.Length
        });
    }

    // POST api/demo/token
    [HttpPost("token")]
    public IActionResult GenerateToken()
    {
        try
        {
            var key = new SymmetricSecurityKey(System.Text.Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(60),
                signingCredentials: credentials);

            return Ok(new Dictionary<string, object?>
            {
                ["token"] = new JwtSecurityTokenHandler().WriteToken(token),
                ["expiresIn"] = 3600
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating token");
            return StatusCode(500, new { error = "Failed to generate token" });
        }
    }
}

