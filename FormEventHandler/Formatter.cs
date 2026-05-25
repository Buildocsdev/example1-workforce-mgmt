namespace FormEventHandler;

public class Formatter
{
    public string FormatDate(DateTime? date, string? format = null)
        => date?.ToString(format ?? "yyyy-MM-dd") ?? string.Empty;

    public string FormatNumber(decimal? value, string? format = null)
        => value?.ToString(format ?? "N2") ?? string.Empty;

    public string FormatString(string? value)
        => value ?? string.Empty;
}
