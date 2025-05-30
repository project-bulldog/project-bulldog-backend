using backend.Infrastructure;
using backend.Services.Interfaces;

public class ReminderCheckerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReminderCheckerService> _logger;
    private readonly ReminderServiceState _state;

    public ReminderCheckerService(IServiceProvider services, ILogger<ReminderCheckerService> logger, ReminderServiceState state)
    {
        _services = services;
        _logger = logger;
        _state = state;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔁 ReminderCheckerService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _services.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IReminderProcessor>();

            _logger.LogInformation("🔎 Checking for due reminders...");

            await processor.ProcessDueRemindersAsync(stoppingToken);

            // Record the last successful run
            _state.LastRunUtc = DateTime.UtcNow;

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
