namespace backend.Helpers;

public static class LogSanitizer
{
    public static string SanitizeForLog(string input)
    {
        return input.Replace("\r", "").Replace("\n", "");
    }
}
