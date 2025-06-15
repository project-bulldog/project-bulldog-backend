namespace backend.Services.FileUpload.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadFileAsync(IFormFile file, string userId);
    Task HandleBlobCleanupAsync(string blobName, bool success);
}
