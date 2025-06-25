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
        {
            // Check if it's already a Windows timezone ID
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                return timeZoneId; // Already a valid Windows timezone ID
            }
            catch (TimeZoneNotFoundException)
            {
                // Not a Windows timezone ID, try to convert from IANA
                try
                {
                    return TZConvert.IanaToWindows(timeZoneId);
                }
                catch
                {
                    // If conversion fails, return the original
                    return timeZoneId;
                }
            }
        }

        return timeZoneId;
    }
}
