namespace backend.Services.Interfaces;

public interface IUploadService
{
    Task UploadUserFileAsync(IFormFile file);
}
