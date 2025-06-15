using System.Net.Http.Headers;
using backend.Services.FileUpload.Interfaces;

namespace backend.Services.FileUpload.Implementations;

public class TextExtractionTriggerService : ITextExtractionTriggerService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<TextExtractionTriggerService> _logger;
    private const int MAX_RETRIES = 3;

    public TextExtractionTriggerService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<TextExtractionTriggerService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task NotifyFunctionAsync(string blobName, string token)
    {
        var functionUrl = _config["AzureBlobStorage:BlobProcessingFunctionUrl"];
        if (string.IsNullOrWhiteSpace(functionUrl))
        {
            _logger.LogError("❌ BlobProcessingFunctionUrl is missing from configuration.");
            throw new InvalidOperationException("BlobProcessingFunctionUrl is not configured.");
        }

        var request = new HttpRequestMessage(HttpMethod.Post, functionUrl)
        {
            Content = JsonContent.Create(new { BlobName = blobName })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        for (var attempt = 1; attempt <= MAX_RETRIES; attempt++)
        {
            try
            {
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("📡 Azure Function successfully notified for blob '{BlobName}'", blobName);
                    return;
                }

                if (attempt < MAX_RETRIES)
                {
                    _logger.LogWarning("⚠️ Attempt {Attempt}/{MaxRetries} failed. Status: {Status}",
                        attempt, MAX_RETRIES, response.StatusCode);
                    await Task.Delay(2000 * attempt);
                }
                else
                {
                    _logger.LogError("❌ All attempts failed for blob '{BlobName}'", blobName);
                    throw new Exception($"Failed to notify function after {MAX_RETRIES} attempts");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error on attempt {Attempt}/{MaxRetries}", attempt, MAX_RETRIES);
                if (attempt == MAX_RETRIES) throw;
                await Task.Delay(2000 * attempt);
            }
        }
    }
}
