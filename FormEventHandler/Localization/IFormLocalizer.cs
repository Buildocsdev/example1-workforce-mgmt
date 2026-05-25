namespace FormEventHandler;

public interface IFormLocalizer
{
    string Translate(string key, string? pluginCode = null, object[]? args = null);
}
