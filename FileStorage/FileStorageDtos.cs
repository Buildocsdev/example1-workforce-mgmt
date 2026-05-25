namespace FileStorage;

public class FileRequestDto
{
    public string FileId { get; set; } = string.Empty;
    public string PluginCode { get; set; } = string.Empty;
    public string FormCode { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
}

public class LinkedFileRequestDto
{
    public string FileId { get; set; } = string.Empty;
}

public class TokenRequestDto
{
    public string Username { get; set; } = string.Empty;
}
