using System.Text.Json;
using backend.Dtos.ActionItems;
using backend.Dtos.AiSummaries;
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

        public async Task<(string summary, List<ActionItemDto> actionItems)> SummarizeAndExtractAsync(string input, string? modelOverride = null)
        {
            if (_useMockData)
            {
                return ("Mock summary", new List<ActionItemDto>
                {
                    new() { Text = "Mock task 1", DueAt = DateTime.UtcNow.AddDays(1), IsDateOnly = true }
                });
            }

            var model = modelOverride ?? _defaultModel;
            var chat = new ChatClient(model: model, apiKey: _apiKey);

            var prompt = $@"
                    You are a task extraction assistant. Read the following meeting notes and respond in JSON format.

                    Your response must include:
                    - A 1–2 sentence summary
                    - A list of 3–5 action items (tasks)

                    For each task, add a `suggested_time` based on the text:
                    - If the text says a specific date like ""June 20"" or ""by Friday"", use that
                    - If it just implies urgency like ""next week"", infer a likely date
                    - If no time is mentioned or it feels like an all-day task, give the date only (e.g., ""June 25, 2025"")
                    - Do NOT invent specific times (like 5:00 PM) unless clearly stated

                    Respond ONLY in this format:

                    {{
                      ""summary"": ""..."",
                      ""actionItems"": [
                        {{ ""text"": ""Task name here"", ""suggested_time"": ""June 20, 2025"" }},
                        {{ ""text"": ""Another task"", ""suggested_time"": null }}
                      ]
                    }}

                    Only respond with valid JSON. Do not include notes, explanation, or formatting errors.

                    --- Start of Input ---
                    {input}
                    --- End of Input ---
                    ";

            var completion = await chat.CompleteChatAsync(prompt);
            var raw = completion.Value.Content[0].Text.ToString().Trim();

            Console.WriteLine("🧠 Raw OpenAI JSON Response:");
            Console.WriteLine(raw);

            AiStructuredResponseDto? aiResponse;
            try
            {
                aiResponse = JsonSerializer.Deserialize<AiStructuredResponseDto>(raw, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                Console.WriteLine("❌ Failed to parse OpenAI JSON response:");
                Console.WriteLine(ex.Message);
                throw new InvalidOperationException("OpenAI response format invalid", ex);
            }

            var summary = aiResponse?.Summary ?? "No summary available";
            var actionItems = new List<ActionItemDto>();

            foreach (var ai in aiResponse?.ActionItems ?? new())
            {
                Console.WriteLine($"🔍 Raw task: {ai.Text} | Raw time: {ai.SuggestedTime}");

                var isDateOnly = IsDateOnlyString(ai.SuggestedTime);
                var dueAt = await ParseSuggestedTime(ai.SuggestedTime);

                if (isDateOnly && dueAt.HasValue)
                {
                    dueAt = dueAt.Value.Date;
                }

                Console.WriteLine($"🗓️ Parsed time: {dueAt} | IsDateOnly: {isDateOnly}");

                actionItems.Add(new ActionItemDto
                {
                    Text = ai.Text,
                    DueAt = dueAt,
                    IsDateOnly = isDateOnly
                });
            }

            foreach (var item in actionItems)
            {
                Console.WriteLine($"📝 Parsed Task: '{item.Text}', DueAt: {item.DueAt}");
            }

            return (summary, actionItems);
        }

        public async Task<string> GetSummaryOnlyAsync(string input, string? modelOverride = null)
        {
            if (_useMockData)
                return "Mock summary";

            var model = modelOverride ?? _defaultModel;
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

        private static bool IsDateOnlyString(string? timeText)
        {
            if (string.IsNullOrWhiteSpace(timeText) || timeText.Equals("null", StringComparison.OrdinalIgnoreCase))
                return false;

            return !timeText.Contains(":", StringComparison.OrdinalIgnoreCase)
                   && !timeText.Contains("am", StringComparison.OrdinalIgnoreCase)
                   && !timeText.Contains("pm", StringComparison.OrdinalIgnoreCase)
                   && !timeText.Contains("at", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<DateTime?> ParseSuggestedTime(string? timeText)
        {
            if (string.IsNullOrEmpty(timeText) || timeText.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            if (DateTime.TryParse(timeText, out DateTime parsedDate))
                return parsedDate;

            if (DateTime.TryParseExact(timeText, "MMMM d, yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime exactDate))
                return exactDate;

            if (DateTime.TryParseExact(timeText, "MMMM d, yyyy 'at' h:mm tt zzz", null, System.Globalization.DateTimeStyles.None, out DateTime dateWithTime))
                return dateWithTime;

            var now = DateTime.UtcNow;

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

            return await ResolveTimeWithGptAsync(timeText);
        }

        private async Task<DateTime?> ResolveTimeWithGptAsync(string rawTime)
        {
            if (string.IsNullOrWhiteSpace(rawTime)) return null;

            var chat = new ChatClient(model: _defaultModel, apiKey: _apiKey);

            var prompt = $"""
Convert this natural language time expression to an exact ISO 8601 timestamp in UTC.
Example: "next Friday at noon" → "2025-06-20T12:00:00Z"

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
                    return resolved.ToUniversalTime();

                Console.WriteLine($"⚠️ GPT fallback failed to parse: {text}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GPT fallback error: {ex.Message}");
                return null;
            }
        }
    }
}
