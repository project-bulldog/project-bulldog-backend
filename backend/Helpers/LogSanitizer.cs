namespace backend.Helpers;

public static class LogSanitizer
{
    /// <summary>
    /// Strips newline characters to prevent log forging.
    /// </summary>
    public static string SanitizeForLog(string input)
    {
        return input.Replace("\r", "").Replace("\n", "");
    }

    /// <summary>
    /// Trims and sanitizes, returning a preview (e.g., last 4 chars of token).
    /// </summary>
    public static string GetSafeTokenPreview(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "null";

        var sanitized = SanitizeForLog(token);
        return sanitized.Length > 8 ? $"...{sanitized[^4..]}" : sanitized;
    }

    /// <summary>
    /// Logs a bool to indicate presence of a value without revealing its contents.
    /// </summary>
    public static string FormatPresence(bool hasValue)
    {
        return hasValue ? "[present]" : "[missing]";
    }
}
