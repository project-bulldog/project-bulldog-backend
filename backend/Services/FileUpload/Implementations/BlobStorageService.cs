using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using backend.Services.FileUpload.Interfaces;

namespace backend.Services.FileUpload.Implementations;

public class BlobStorageService : IBlobStorageService
{
    private const string UPLOADS_CONTAINER = "uploads";
    private const string DEAD_LETTER_CONTAINER = "dead-letter";
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(BlobServiceClient blobServiceClient, ILogger<BlobStorageService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(IFormFile file, string userId)
    {
        var fileNameSanitized = Path.GetFileName(file.FileName);
        var blobName = $"{userId}/{Guid.NewGuid()}_{fileNameSanitized}";
        var container = _blobServiceClient.GetBlobContainerClient(UPLOADS_CONTAINER);

        await container.CreateIfNotExistsAsync();
        var blob = container.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        var uploadOptions = new BlobUploadOptions
        {
            Metadata = new Dictionary<string, string>
            {
                { "originalfilename", fileNameSanitized },
                { "contenttype", file.ContentType }
            },
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = file.ContentType
            }
        };

        try
        {
            await blob.UploadAsync(stream, uploadOptions);
            _logger.LogInformation("✅ File uploaded to blob '{BlobName}' (URL: {Url})", blobName, blob.Uri);
            return blobName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to upload file to blob storage.");
            throw;
        }
    }

    public async Task HandleBlobCleanupAsync(string blobName, bool success)
    {
        var sourceBlob = _blobServiceClient.GetBlobContainerClient(UPLOADS_CONTAINER).GetBlobClient(blobName);

        if (!success)
        {
            var deadLetterContainer = _blobServiceClient.GetBlobContainerClient(DEAD_LETTER_CONTAINER);
            await deadLetterContainer.CreateIfNotExistsAsync();
            var deadLetterBlob = deadLetterContainer.GetBlobClient(blobName);
            await deadLetterBlob.StartCopyFromUriAsync(sourceBlob.Uri);
            _logger.LogWarning("☠️ Moved blob {name} to dead-letter container", blobName);
        }

        await sourceBlob.DeleteIfExistsAsync();
        _logger.LogInformation("🗑️ Deleted blob {name} after processing", blobName);
    }
}
