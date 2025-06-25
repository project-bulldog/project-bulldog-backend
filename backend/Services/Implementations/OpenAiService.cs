using System.Text.Json;
using backend.Dtos.ActionItems;
using backend.Dtos.AiSummaries;
using backend.Helpers;
using backend.Services.Interfaces;
using OpenAI.Chat;


namespace backend.Services.Implementations
{
    public class OpenAiService : IOpenAiService
    {
        private readonly bool _useMockData;
        private readonly string _defaultModel;
        private readonly string _apiKey;

        public OpenAiService(IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _useMockData = config.GetValue<bool>("OpenAI:UseMock");
            _defaultModel = config["OpenAI:Model"] ?? "gpt-3.5-turbo";
            _apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key is not configured");
        }

        public async Task<(string summary, List<ActionItemDto> actionItems)> SummarizeAndExtractAsync(string input, string modelOverride, string userTimeZoneId)
        {
            if (_useMockData)
            {
                return ("Mock summary", new List<ActionItemDto>
        {
            new() { Text = "Mock task 1", DueAt = DateTime.UtcNow.AddDays(1), IsDateOnly = true }
        });
            }

            var model = string.IsNullOrWhiteSpace(modelOverride) ? _defaultModel : modelOverride;

            var chat = new ChatClient(model: model, apiKey: _apiKey);
            var prompt = BuildSummarizationPrompt(input);

            var completion = await chat.CompleteChatAsync(prompt);
            var raw = completion.Value.Content[0].Text.ToString().Trim();

            Console.WriteLine("🧠 Raw OpenAI JSON Response:\n" + raw);

            var cleanedJson = CleanMarkdownJson(raw);
            Console.WriteLine("🧹 Cleaned JSON:\n" + cleanedJson);

            var (summary, actionItems) = await ParseAiStructuredResponse(cleanedJson, userTimeZoneId);
            return (summary, actionItems);
        }

        public async Task<string> GetSummaryOnlyAsync(string input, string modelOverride)
        {
            if (_useMockData)
                return "Mock summary";

            var model = string.IsNullOrWhiteSpace(modelOverride) ? _defaultModel : modelOverride;

            var chat = new ChatClient(model: model, apiKey: _apiKey);

            var prompt = $"""
                Summarize the following text in 2–4 sentences:

                {input}
                """;

            var completion = await chat.CompleteChatAsync(prompt);
            var raw = completion.Value.Content[0].Text.Trim();

            Console.WriteLine("🧠 Raw OpenAI Response - Summary Only:");
            Console.WriteLine(raw);

            return raw;
        }

        #region Private Methods
        private static string BuildSummarizationPrompt(string input) => $@"
            You are a task extraction assistant. Read the following meeting notes and respond in JSON format.

            Your response must include:
            - A 1–2 sentence summary
            - A list of 3–5 action items (tasks)

            For each task, add a `suggested_time` based on the text:
            - If the text says a specific date like ""June 20"" or ""by Friday"", use that
            - If it just implies urgency like ""next week"", infer a likely date
            - If no time is mentioned or it feels like an all-day task, give the date only (e.g., ""June 25, 2025"")
            - Do NOT invent specific times (like 5:00 PM) unless clearly stated

            IMPORTANT: When you see timezone abbreviations (like PT, ET, CT, MT, GMT, UTC, etc.) or specific times with timezone context:
            - Preserve the timezone information in your suggested_time
            - If you see ""9:45 PM MT"" or ""By 9:45 PM MT"", include the timezone: ""June 23, 2025 at 9:45 PM MT""
            - If you see ""10:15 PM ET"", include it as ""June 23, 2025 at 10:15 PM ET""
            - If you see ""2:00 PM PT"", include it as ""June 23, 2025 at 2:00 PM PT""
            - Always preserve the original timezone context when present (PT, ET, CT, MT, GMT, UTC, etc.)

            Respond ONLY in this format:

            {{
              ""summary"": ""..."",
              ""actionItems"": [
                {{ ""text"": ""Task name here"", ""suggested_time"": ""June 20, 2025 at 9:45 PM MT"" }},
                {{ ""text"": ""Another task"", ""suggested_time"": null }}
              ]
            }}

            Only respond with valid JSON. Do not include notes, explanation, or formatting errors.

            --- Start of Input ---
            {input}
            --- End of Input ---
            ";

        private static string CleanMarkdownJson(string raw)
        {
            var json = raw.Trim();

            if (json.StartsWith("```")) json = json.Substring(json.IndexOf('\n') + 1);
            if (json.EndsWith("```")) json = json[..^3].Trim();

            if (json.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                json = json.Substring(4).Trim();

            var idx = json.IndexOfAny(new[] { '{', '[' });
            return idx >= 0 ? json.Substring(idx).Trim() : json;
        }

        private async Task<(string summary, List<ActionItemDto>)> ParseAiStructuredResponse(string cleanedJson, string userTimeZoneId)
        {
            AiStructuredResponseDto? aiResponse;
            try
            {
                aiResponse = JsonSerializer.Deserialize<AiStructuredResponseDto>(cleanedJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? throw new InvalidOperationException("Empty or malformed AI response");
            }
            catch (JsonException ex)
            {
                Console.WriteLine("❌ Failed to parse OpenAI JSON response:\n" + ex.Message);
                throw new InvalidOperationException("OpenAI response format invalid", ex);
            }

            var summary = aiResponse.Summary ?? "No summary available";
            var actionItems = new List<ActionItemDto>();

            foreach (var ai in aiResponse.ActionItems ?? new())
            {
                Console.WriteLine($"🔍 Raw task: {ai.Text} | Raw time: {ai.SuggestedTime}");

                var isDateOnly = IsDateOnlyString(ai.SuggestedTime);
                var dueAt = await ParseSuggestedTime(ai.SuggestedTime, userTimeZoneId);

                if (isDateOnly && dueAt.HasValue)
                    dueAt = dueAt.Value.Date;

                actionItems.Add(new ActionItemDto
                {
                    Text = ai.Text,
                    DueAt = dueAt,
                    IsDateOnly = isDateOnly
                });

                Console.WriteLine($"📝 Parsed Task: '{ai.Text}', DueAt: {dueAt}");
            }

            return (summary, actionItems);
        }

        private static bool IsDateOnlyString(string? timeText)
        {
            if (string.IsNullOrWhiteSpace(timeText) || timeText.Equals("null", StringComparison.OrdinalIgnoreCase))
                return false;

            return !timeText.Contains(":", StringComparison.OrdinalIgnoreCase)
                   && !timeText.Contains("am", StringComparison.OrdinalIgnoreCase)
                   && !timeText.Contains("pm", StringComparison.OrdinalIgnoreCase)
                   && !timeText.Contains("at", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<DateTime?> ParseSuggestedTime(string? timeText, string? userTimeZoneId = null)
        {
            if (string.IsNullOrEmpty(timeText) || timeText.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            Console.WriteLine($"🔍 Parsing time: '{timeText}'");

            // Try to parse with timezone information first
            if (timeText.Contains(" at ", StringComparison.OrdinalIgnoreCase) &&
                (timeText.Contains("AM", StringComparison.OrdinalIgnoreCase) ||
                 timeText.Contains("PM", StringComparison.OrdinalIgnoreCase)))
            {
                // Check for common timezone abbreviations
                var timezoneAbbreviations = new[] { "PT", "PST", "PDT", "ET", "EST", "EDT", "CT", "CST", "CDT", "MT", "MST", "MDT", "GMT", "UTC", "Z" };
                var foundTimezone = timezoneAbbreviations.FirstOrDefault(tz => timeText.Contains(tz, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(foundTimezone))
                {
                    Console.WriteLine($"✅ Found timezone abbreviation: {foundTimezone}");

                    // Try to get the timezone info
                    TimeZoneInfo? timezone = null;
                    try
                    {
                        switch (foundTimezone.ToUpper())
                        {
                            case "PT":
                            case "PST":
                            case "PDT":
                                timezone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
                                break;
                            case "ET":
                            case "EST":
                            case "EDT":
                                timezone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                                break;
                            case "CT":
                            case "CST":
                            case "CDT":
                                timezone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
                                break;
                            case "MT":
                            case "MST":
                            case "MDT":
                                timezone = TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
                                break;
                            case "GMT":
                            case "UTC":
                            case "Z":
                                // These are already UTC, so we can parse directly
                                timezone = TimeZoneInfo.Utc;
                                break;
                        }
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        Console.WriteLine($"⚠️ Timezone {foundTimezone} not found on this system");
                    }

                    if (timezone != null)
                    {
                        // Try various formats with the detected timezone
                        var formats = new[]
                        {
                            $"MMMM d, yyyy 'at' h:mm tt {foundTimezone}",
                            $"MMMM d, yyyy 'at' h:mm tt '{foundTimezone}'",
                            $"MMMM d, yyyy 'at' h:mm tt"
                        };

                        foreach (var format in formats)
                        {
                            if (DateTime.TryParseExact(timeText, format, null, System.Globalization.DateTimeStyles.None, out DateTime dateWithTimezone))
                            {
                                // Convert to UTC
                                var utcTime = timezone == TimeZoneInfo.Utc ? dateWithTimezone : TimeZoneInfo.ConvertTimeToUtc(dateWithTimezone, timezone);
                                Console.WriteLine($"✅ Parsed {foundTimezone} time: {dateWithTimezone} → UTC: {utcTime}");
                                return utcTime;
                            }
                        }
                    }
                }
            }

            // Try standard DateTime parsing
            if (DateTime.TryParse(timeText, out DateTime parsedDate))
            {
                Console.WriteLine($"✅ Standard parse: {parsedDate}");

                // If we have a user timezone and the parsed date doesn't have timezone info, assume it's in the user's timezone
                if (!string.IsNullOrWhiteSpace(userTimeZoneId) && parsedDate.Kind == DateTimeKind.Unspecified)
                {
                    try
                    {
                        // Use the normalized timezone helper
                        var tzId = TimeZoneHelpers.NormalizeTimeZoneId(userTimeZoneId);
                        var userTimezone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                        var utcTime = TimeZoneInfo.ConvertTimeToUtc(parsedDate, userTimezone);
                        Console.WriteLine($"✅ Converted from user timezone {userTimeZoneId}: {parsedDate} → UTC: {utcTime}");
                        return utcTime;
                    }
                    catch (TimeZoneNotFoundException)
                    {
                        Console.WriteLine($"⚠️ User timezone {userTimeZoneId} not found, using parsed date as-is");
                    }
                }

                return parsedDate;
            }

            // Try date-only format
            if (DateTime.TryParseExact(timeText, "MMMM d, yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime exactDate))
            {
                Console.WriteLine($"✅ Date-only parse: {exactDate}");
                return exactDate;
            }

            // Try format with timezone abbreviation
            if (DateTime.TryParseExact(timeText, "MMMM d, yyyy 'at' h:mm tt zzz", null, System.Globalization.DateTimeStyles.None, out DateTime dateWithTime))
            {
                Console.WriteLine($"✅ Timezone parse: {dateWithTime}");
                return dateWithTime;
            }

            var now = DateTime.UtcNow;

            // Handle relative time expressions
            if (timeText.Contains("tomorrow", StringComparison.OrdinalIgnoreCase))
                return now.AddDays(1);
            if (timeText.Contains("next week", StringComparison.OrdinalIgnoreCase))
                return now.AddDays(7);
            if (timeText.Contains("next month", StringComparison.OrdinalIgnoreCase))
                return now.AddMonths(1);
            if (timeText.Contains("by friday", StringComparison.OrdinalIgnoreCase))
            {
                var daysUntilFriday = ((int)DayOfWeek.Friday - (int)now.DayOfWeek + 7) % 7;
                return now.AddDays(daysUntilFriday);
            }
            if (timeText.Contains("in 2 days", StringComparison.OrdinalIgnoreCase))
                return now.AddDays(2);
            if (timeText.Contains("in 3 days", StringComparison.OrdinalIgnoreCase))
                return now.AddDays(3);
            if (timeText.Contains("in a week", StringComparison.OrdinalIgnoreCase))
                return now.AddDays(7);
            if (timeText.Contains("in two weeks", StringComparison.OrdinalIgnoreCase))
                return now.AddDays(14);
            if (timeText.Contains("end of month", StringComparison.OrdinalIgnoreCase))
                return new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
            if (timeText.Contains("end of week", StringComparison.OrdinalIgnoreCase))
            {
                var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;
                return now.AddDays(daysUntilSunday);
            }

            Console.WriteLine($"⚠️ Could not parse suggested time: '{timeText}'");

            return await ResolveTimeWithGptAsync(timeText, userTimeZoneId);
        }

        private async Task<DateTime?> ResolveTimeWithGptAsync(string rawTime, string? userTimeZoneId = null)
        {
            if (string.IsNullOrWhiteSpace(rawTime)) return null;

            var chat = new ChatClient(model: _defaultModel, apiKey: _apiKey);

            var prompt = $"""
                Convert this natural language time expression to an exact ISO 8601 timestamp in UTC.
                Pay special attention to timezone abbreviations like PT (Pacific Time), ET (Eastern Time), CT (Central Time), MT (Mountain Time), GMT (Greenwich Mean Time), UTC (Coordinated Universal Time), etc.

                Examples:
                - "June 23, 2025 at 9:45 PM MT" → "2025-06-24T03:45:00Z" (Mountain Time to UTC)
                - "June 23, 2025 at 10:15 PM ET" → "2025-06-24T02:15:00Z" (Eastern Time to UTC)
                - "June 23, 2025 at 2:00 PM PT" → "2025-06-23T21:00:00Z" (Pacific Time to UTC)
                - "next Friday at noon" → "2025-06-20T12:00:00Z"
                - "June 20, 2025" → "2025-06-20T00:00:00Z"

                Respond with **only** the timestamp. If unknown or ambiguous, say "null".

                Expression: {rawTime}
                """;

            try
            {
                var result = await chat.CompleteChatAsync(prompt);
                var text = result.Value.Content[0].Text.Trim();

                if (text.Equals("null", StringComparison.OrdinalIgnoreCase))
                    return null;

                if (DateTime.TryParse(text, out var resolved))
                {
                    Console.WriteLine($"✅ GPT fallback resolved: {rawTime} → {resolved}");
                    return resolved.ToUniversalTime();
                }

                Console.WriteLine($"⚠️ GPT fallback failed to parse: {text}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GPT fallback error: {ex.Message}");
                return null;
            }
        }
        #endregion
    }
}
