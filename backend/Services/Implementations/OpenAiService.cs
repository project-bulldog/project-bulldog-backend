using OpenAI.Chat;

namespace backend.Services.Implementations
{
    public class OpenAIService
    {
        private readonly ChatClient? _chat;
        private readonly bool _useMock;

        public OpenAIService(IConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _useMock = config.GetValue<bool>("OpenAI:UseMock");

            if (!_useMock)
            {
                // Pull model name & key from config
                var model = config["OpenAI:Model"] ?? "gpt-3.5-turbo";
                var apiKey = config["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key is not configured");
                _chat = new ChatClient(model: model, apiKey: apiKey);
            }
        }

        public async Task<(string summary, List<string> tasks)> SummarizeAndExtractAsync(string input)
        {
            if (_useMock)
                return ("Mock summary", new() { "Mock task 1", "Mock task 2" });

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

            var completion = await _chat.CompleteChatAsync(prompt);

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

    }
}
