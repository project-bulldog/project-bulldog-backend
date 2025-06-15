using backend.Services.Auth.Interfaces;
using backend.Services.FileUpload.Interfaces;

namespace backend.Services.FileUpload.Implementations;

public class UploadService : IUploadService
{
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ITextExtractionTriggerService _functionNotificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UploadService> _logger;

    public UploadService(
        ICurrentUserProvider currentUserProvider,
        IBlobStorageService blobStorageService,
        ITextExtractionTriggerService functionNotificationService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UploadService> logger)
    {
        _currentUserProvider = currentUserProvider;
        _blobStorageService = blobStorageService;
        _functionNotificationService = functionNotificationService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task UploadUserFileAsync(IFormFile file)
    {
        var userId = _currentUserProvider.UserId;
        var token = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString()?.Replace("Bearer ", "");

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("⚠️ No Authorization token found in request headers.");
            throw new UnauthorizedAccessException("Missing token.");
        }

        var blobName = await _blobStorageService.UploadFileAsync(file, userId.ToString());
        try
        {
            await _functionNotificationService.NotifyFunctionAsync(blobName, token);
            await _blobStorageService.HandleBlobCleanupAsync(blobName, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to process blob {BlobName}", blobName);
            await _blobStorageService.HandleBlobCleanupAsync(blobName, false);
            throw;
        }
    }
}
