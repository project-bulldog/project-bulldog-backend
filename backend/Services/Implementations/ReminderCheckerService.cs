using backend.Services.Interfaces;

public class ReminderCheckerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ReminderCheckerService> _logger;

    public ReminderCheckerService(IServiceProvider services, ILogger<ReminderCheckerService> logger)
    {
        _services = services;
        _logger = logger;
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

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
