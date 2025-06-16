using System.Net.Http.Headers;
using backend.Dtos.AiSummaries;
using backend.Services.Auth.Interfaces;
using backend.Services.FileUpload.Interfaces;

namespace backend.Services.FileUpload.Implementations;

public class UploadService : IUploadService
{
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IBlobStorageService _blobStorageService;
    private readonly HttpClient _httpClient;
    private readonly string _functionUrl;
    private readonly IHttpContextAccessor _httpCtx;

    public UploadService(
        ICurrentUserProvider currentUserProvider,
        IBlobStorageService blobStorageService,
        HttpClient httpClient,
        IConfiguration config,
        IHttpContextAccessor httpCtx)
    {
        _currentUserProvider = currentUserProvider;
        _blobStorageService = blobStorageService;
        _httpClient = httpClient;
        _httpCtx = httpCtx;
        _functionUrl = config["AzureBlobStorage:BlobProcessingFunctionUrl"]
            ?? throw new InvalidOperationException("Missing AzureBlobStorage:BlobProcessingFunctionUrl configuration");
    }

    public async Task<AiSummaryWithTasksResponseDto> UploadUserFileAsync(IFormFile file)
    {
        // 1) get user & token
        var userId = _currentUserProvider.UserId.ToString();
        var token = _httpCtx.HttpContext?.Request.Headers.Authorization.ToString().Replace("Bearer ", "")
            ?? throw new InvalidOperationException("Missing authorization header");

        // 2) upload blob
        var blobName = await _blobStorageService.UploadFileAsync(file, userId);

        // 3) call function
        var req = new HttpRequestMessage(HttpMethod.Post, _functionUrl)
        {
            Content = JsonContent.Create(new { BlobName = blobName, UserToken = token })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var res = await _httpClient.SendAsync(req);
        res.EnsureSuccessStatusCode();

        // 4) parse and return summary+tasks
        return await res.Content.ReadFromJsonAsync<AiSummaryWithTasksResponseDto>()
               ?? throw new InvalidOperationException("Empty response from function");
    }
}
