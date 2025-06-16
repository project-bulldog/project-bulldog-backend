using backend.Dtos.AiSummaries;

namespace backend.Services.FileUpload.Interfaces;

public interface IUploadService
{
    Task<AiSummaryWithTasksResponseDto> UploadUserFileAsync(IFormFile file);
}
