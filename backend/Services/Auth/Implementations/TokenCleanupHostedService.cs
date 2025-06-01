namespace backend.Services.Auth.Implementations;

using backend.Services.Auth.Interfaces;
using Microsoft.Extensions.Hosting;

public class TokenCleanupHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TokenCleanupHostedService> _logger;

    public TokenCleanupHostedService(IServiceProvider services, ILogger<TokenCleanupHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TokenCleanupHostedService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<ITokenCleanupService>();

                _logger.LogInformation("Running token cleanup job...");
                await cleanupService.CleanupExpiredTokensAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token cleanup");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

}
