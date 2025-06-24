using backend.Data;
using backend.Dtos.ActionItems;
using backend.Dtos.AiSummaries;
using backend.Helpers;
using backend.Mappers;
using backend.Models;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;
using SharpToken;

namespace backend.Services.Implementations
{
    public class AiService : IAiService
    {
        private const string DefaultModel = "gpt-4-turbo";
        private readonly int _maxTokensPerChunk;

        private readonly BulldogDbContext _context;
        private readonly ICurrentUserProvider _currentUserProvider;
        private readonly IOpenAiService _openAiService;
        private readonly IUserService _userService;
        private readonly ILogger<AiService> _logger;

        public AiService(
            BulldogDbContext context,
            ICurrentUserProvider currentUserProvider,
            IOpenAiService openAiService,
            IUserService userService,
            ILogger<AiService> logger,
            int maxTokensPerChunk = 120_000)
        {
            _context = context;
            _currentUserProvider = currentUserProvider;
            _openAiService = openAiService;
            _userService = userService;
            _logger = logger;
            _maxTokensPerChunk = maxTokensPerChunk;
        }

        public async Task<AiSummaryResponseDto> SummarizeAsync(CreateAiSummaryRequestDto request, string? userTimeZoneId = null)
        {
            var userId = _currentUserProvider.UserId;
            userTimeZoneId ??= await GetCurrentUserTimeZoneIdAsync();

            var (summaryText, actionItemDtos) = await _openAiService.SummarizeAndExtractAsync(
                request.InputText,
                string.Empty,
                userTimeZoneId ?? string.Empty
            );

            var actionItems = actionItemDtos.Select(dto => new ActionItem
            {
                Id = Guid.NewGuid(),
                Text = dto.Text,
                DueAt = dto.DueAt,
                IsDone = false,
                IsDateOnly = dto.IsDateOnly
            }).ToList();

            var summary = BuildSummary(request.InputText, summaryText, actionItems, userTimeZoneId);

            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync();

            return new AiSummaryResponseDto
            {
                Summary = SummaryMapper.ToDto(summary)
            };
        }

        public async Task<string> SummarizeChunkedAsync(AiChunkedSummaryResponseDto request)
        {
            var model = request.Model ?? DefaultModel;
            var useMapReduce = request.UseMapReduce ?? true;

            // Count tokens, decide whether to chunk
            var encoder = GptEncoding.GetEncodingForModel(model);
            var totalTokens = encoder.CountTokens(request.Input);

            if (model == "gpt-4-turbo" && totalTokens < _maxTokensPerChunk)
            {
                return await _openAiService.GetSummaryOnlyAsync(request.Input, model);
            }

            var chunks = ChunkByTokens(request.Input, encoder, _maxTokensPerChunk);
            var summaries = new List<string>();

            _logger.LogInformation($"[Chunked] Total input tokens: {totalTokens}");
            _logger.LogInformation($"[Chunked] Chunks created: {chunks.Count}");

            foreach (var chunk in chunks)
            {
                summaries.Add(await _openAiService.GetSummaryOnlyAsync(chunk, model));
            }

            if (!useMapReduce)
            {
                return string.Join("\n\n", summaries);
            }

            var stitchedSummaryInput = string.Join("\n\n", summaries);
            return await _openAiService.GetSummaryOnlyAsync($"Summarize this combined summary:\n{stitchedSummaryInput}", model);
        }

        public async Task<(string summary, List<ActionItemDto> actionItems)> SummarizeAndExtractActionItemsChunkedAsync(AiChunkedSummaryResponseDto request)
        {
            var model = request.Model ?? DefaultModel;
            var useMapReduce = request.UseMapReduce ?? true;

            var userTimeZoneId = await GetCurrentUserTimeZoneIdAsync() ?? string.Empty;

            var encoder = GptEncoding.GetEncodingForModel(model);
            var totalTokens = encoder.CountTokens(request.Input);

            if (model == "gpt-4-turbo" && totalTokens < _maxTokensPerChunk)
            {
                return await _openAiService.SummarizeAndExtractAsync(request.Input, model, userTimeZoneId);
            }

            var chunks = ChunkByTokens(request.Input, encoder, _maxTokensPerChunk);
            var summaries = new List<string>();
            var allTasks = new List<ActionItemDto>();

            _logger.LogInformation($"[Extract] Total tokens: {totalTokens}");
            _logger.LogInformation($"[Extract] Chunk count: {chunks.Count}");

            foreach (var chunk in chunks)
            {
                var (summary, actionItems) = await _openAiService.SummarizeAndExtractAsync(chunk, model, userTimeZoneId);

                if (!string.IsNullOrEmpty(summary))
                {
                    summaries.Add(summary);
                }
                allTasks.AddRange(actionItems);
            }

            if (!useMapReduce || summaries.Count == 0)
            {
                var joinedSummary = string.Join("\n\n", summaries);
                return (string.IsNullOrEmpty(joinedSummary) ? "No summary available" : joinedSummary, allTasks);
            }

            var stitchedSummaryInput = string.Join("\n\n", summaries);
            var finalSummary = await _openAiService.GetSummaryOnlyAsync($"Summarize this combined summary:\n{stitchedSummaryInput}", model);

            return (string.IsNullOrEmpty(finalSummary) ? "No summary available" : finalSummary, allTasks);
        }

        public async Task<AiSummaryResponseDto> SummarizeAndSaveChunkedAsync(AiChunkedSummaryResponseDto request)
        {
            var userId = _currentUserProvider.UserId;
            var userTimeZoneId = await GetCurrentUserTimeZoneIdAsync();

            var (summaryText, actionItemDtos) = await SummarizeAndExtractActionItemsChunkedAsync(request);

            var actionItems = actionItemDtos.Select(dto =>
            {
                _logger.LogInformation("📋 From AI DTO → Text='{Text}', DueAt={DueAt}, IsDateOnly={IsDateOnly}", dto.Text, dto.DueAt, dto.IsDateOnly);
                return new ActionItem
                {
                    Id = Guid.NewGuid(),
                    Text = dto.Text,
                    DueAt = dto.DueAt,
                    IsDone = false,
                    IsDateOnly = dto.IsDateOnly
                };
            }).ToList();

            var summary = BuildSummary(request.Input, summaryText, actionItems, userTimeZoneId);

            foreach (var item in summary.ActionItems)
            {
                _logger.LogInformation("📦 Ready to Save → Text='{Text}', DueAt={DueAt}, IsDateOnly={IsDateOnly}",
                    item.Text, item.DueAt, item.IsDateOnly);
            }

            _context.Summaries.Add(summary);
            await _context.SaveChangesAsync();

            return new AiSummaryResponseDto
            {
                Summary = SummaryMapper.ToDto(summary)
            };
        }

        #region Private Methods
        private static List<string> ChunkByTokens(string input, GptEncoding encoder, int maxTokens)
        {
            var chunks = new List<string>();
            var tokens = encoder.Encode(input);
            var totalTokens = tokens.Count;
            var cursor = 0;

            while (cursor < totalTokens)
            {
                var takeCount = Math.Min(maxTokens, totalTokens - cursor);
                var slice = tokens.Skip(cursor).Take(takeCount).ToArray();
                var chunk = encoder.Decode(slice);
                chunks.Add(chunk);
                cursor += takeCount;
            }

            return chunks;
        }

        private DateTime GetUserLocalTime(DateTime utcNow, string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
                return utcNow;

            try
            {
                var tzId = TimeZoneHelpers.NormalizeTimeZoneId(timeZoneId);
                var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
                return TimeZoneInfo.ConvertTimeFromUtc(utcNow, userTimeZone);
            }
            catch (TimeZoneNotFoundException)
            {
                _logger.LogWarning("User timezone {Timezone} not found, using UTC for local time", timeZoneId);
                return utcNow;
            }
        }

        private async Task<string?> GetCurrentUserTimeZoneIdAsync()
        {
            var user = await _userService.GetUserEntityAsync(_currentUserProvider.UserId);
            return user?.TimeZoneId;
        }

        private Summary BuildSummary(string originalText, string summaryText, List<ActionItem> actionItems, string? timeZoneId)
        {
            var utcNow = DateTime.UtcNow;
            var localNow = GetUserLocalTime(utcNow, timeZoneId);

            return new Summary
            {
                Id = Guid.NewGuid(),
                UserId = _currentUserProvider.UserId,
                CreatedAtUtc = utcNow,
                CreatedAtLocal = localNow,
                OriginalText = originalText,
                SummaryText = summaryText,
                ActionItems = actionItems
            };
        }

        #endregion
    }
}
