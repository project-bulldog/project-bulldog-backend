using backend.Services.Interfaces;
using OpenAI.Chat;

namespace backend.Services.Implementations
{
    public class OpenAiService : IOpenAiService
    {
        private readonly bool _useMockData; //This is so we can run Ai Service tests without hitting the OpenAI API and spending money.
        private readonly string _defaultModel;
        private readonly string _apiKey;

        public OpenAiService(IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _useMockData = config.GetValue<bool>("OpenAI:UseMock");
            _defaultModel = config["OpenAI:Model"] ?? "gpt-3.5-turbo";
            _apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key is not configured");
        }

        public async Task<(string summary, List<string> tasks)> SummarizeAndExtractAsync(string input, string modelOverride = null)
        {
            if (_useMockData)
                return ("Mock summary", new() { "Mock task 1", "Mock task 2" });

            var model = modelOverride ?? _defaultModel;
            var chat = new ChatClient(model: model, apiKey: _apiKey); // New client with override if needed

            var prompt = $"""
                        You are a task assistant. Summarize the following text in 1–2 sentences, then extract up to 5 tasks.

                        Respond **exactly** in this format:

                        Summary: [your summary here]
                        Action Items:
                        - task 1
                        - task 2
                        - ...

                        ---
                        Input:
                        {input}
                        """;

            var completion = await chat.CompleteChatAsync(prompt);
            var raw = completion.Value.Content[0].Text.ToString().Trim();

            // 🔍 Log raw output for debugging
            Console.WriteLine("🧠 Raw OpenAI Response:");
            Console.WriteLine(raw);

            // 🛡️ Defensive parsing
            string summary = "No summary available";
            var tasks = new List<string>();

            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("Summary:", StringComparison.OrdinalIgnoreCase))
                {
                    summary = line["Summary:".Length..].Trim();
                }
                else if (line.StartsWith("- "))
                {
                    tasks.Add(line[2..].Trim());
                }
            }

            return (summary, tasks);
        }

        public async Task<string> GetSummaryOnlyAsync(string input, string modelOverride = null)
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
            var raw = completion.Value.Content[0].Text.ToString().Trim();

            Console.WriteLine("🧠 Raw OpenAI Response - Summary Only:");
            Console.WriteLine(raw);

            return raw;
        }
    }

}
