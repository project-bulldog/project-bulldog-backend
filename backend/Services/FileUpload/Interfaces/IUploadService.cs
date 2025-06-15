namespace backend.Services.FileUpload.Interfaces;

public interface IUploadService
{
    Task UploadUserFileAsync(IFormFile file);
}
