namespace backend.Services.FileUpload.Interfaces;

public interface ITextExtractionTriggerService
{
    Task NotifyFunctionAsync(string blobName, string token);
}
