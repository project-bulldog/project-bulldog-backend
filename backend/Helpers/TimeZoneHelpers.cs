using System.Runtime.InteropServices;
using TimeZoneConverter;

namespace backend.Helpers;

public static class TimeZoneHelpers
{
    public static DateTime ConvertToLocal(DateTime utc, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return utc;

        try
        {
            var tzId = NormalizeTimeZoneId(timeZoneId);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        }
        catch
        {
            return utc;
        }
    }

    public static DateTime ConvertToUtc(DateTime localTime, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return localTime;

        try
        {
            var tzId = NormalizeTimeZoneId(timeZoneId);
            var zone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
            return TimeZoneInfo.ConvertTimeToUtc(localTime, zone);
        }
        catch
        {
            return localTime;
        }
    }

    public static string NormalizeTimeZoneId(string timeZoneId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return TZConvert.IanaToWindows(timeZoneId);

        return timeZoneId;
    }
}
