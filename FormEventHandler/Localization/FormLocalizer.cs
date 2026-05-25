using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace FormEventHandler;

public class FormLocalizer : IFormLocalizer
{
    private readonly string _root;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();

    public FormLocalizer(IHttpContextAccessor httpContextAccessor, string? root = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _root = root ?? Path.Combine(AppContext.BaseDirectory, "Localization");
    }

    public string Translate(string key, string? pluginCode = null, object[]? args = null)
    {
        var cultureName = GetRequestCulture();

        foreach (var lang in CultureFallback(cultureName))
        {
            var dict = Load(lang, pluginCode);
            if (dict.TryGetValue(key, out var value))
                return args?.Length > 0 ? string.Format(value, args) : value;
        }

        return key;
    }

    private string GetRequestCulture()
    {
        var header = _httpContextAccessor.HttpContext?.Request.Headers["Accept-Language"].ToString();
        if (!string.IsNullOrEmpty(header))
        {
            // "et-EE,et;q=0.9,en;q=0.8" → "et-EE"
            var primary = header.Split(',')[0].Split(';')[0].Trim();
            if (!string.IsNullOrEmpty(primary))
                return primary;
        }
        return Thread.CurrentThread.CurrentUICulture.Name;
    }

    private Dictionary<string, string> Load(string culture, string? pluginCode)
    {
        var cacheKey = $"{pluginCode ?? string.Empty}:{culture}";
        return _cache.GetOrAdd(cacheKey, _ =>
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            Merge(merged, Path.Combine(_root, $"{culture}.json"));

            if (!string.IsNullOrEmpty(pluginCode))
                Merge(merged, Path.Combine(_root, "Plugins", pluginCode, $"{culture}.json"));

            return merged;
        });
    }

    private static void Merge(Dictionary<string, string> target, string filePath)
    {
        if (!File.Exists(filePath)) return;
        var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath));
        if (dict == null) return;
        foreach (var kv in dict) target[kv.Key] = kv.Value;
    }

    // "fr-FR" → ["fr-FR", "fr", "en"]  |  "en-US" → ["en-US", "en"]  |  "et" → ["et", "en"]
    private static IEnumerable<string> CultureFallback(string cultureName)
    {
        yield return cultureName;

        int dash = cultureName.IndexOf('-');
        if (dash > 0)
        {
            var parent = cultureName[..dash];
            yield return parent;
        }

        if (!cultureName.Equals("en", StringComparison.OrdinalIgnoreCase) &&
            !cultureName.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
        {
            yield return "en";
        }
    }
}
