using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using backend.Services.Auth.Interfaces;
using backend.Services.Interfaces;

namespace backend.Services.Implementations;

public class UploadService : IUploadService
{
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UploadService> _logger;

    public UploadService(
        ICurrentUserProvider currentUserProvider,
        BlobServiceClient blobServiceClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UploadService> logger)
    {
        _currentUserProvider = currentUserProvider;
        _blobServiceClient = blobServiceClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task UploadUserFileAsync(IFormFile file)
    {
        var userId = _currentUserProvider.UserId;
        var blobName = $"{userId}/{Guid.NewGuid()}_{file.FileName}";
        var container = _blobServiceClient.GetBlobContainerClient("uploads");

        await container.CreateIfNotExistsAsync();
        var blob = container.GetBlobClient(blobName);

        var token = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString()?.Replace("Bearer ", "");
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("⚠️ No Authorization token found in request headers.");
            throw new UnauthorizedAccessException("Missing token.");
        }

        await using var stream = file.OpenReadStream();

        var uploadOptions = new BlobUploadOptions
        {
            Metadata = new Dictionary<string, string>
            {
                { "authorization", $"Bearer {token}" }
            }
        };

        await blob.UploadAsync(stream, uploadOptions);

        _logger.LogInformation("✅ File uploaded to blob with metadata: {BlobName}", blobName);
    }
}
