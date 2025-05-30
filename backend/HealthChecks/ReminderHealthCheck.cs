using backend.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace backend.HealthChecks;
public class ReminderHealthCheck : IHealthCheck
{
    private readonly ReminderServiceState _state;

    public ReminderHealthCheck(ReminderServiceState state)
    {
        _state = state;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _state.LastRunUtc;

        if (_state.LastRunUtc == DateTime.MinValue)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Reminder service has never run."));
        }

        if (elapsed > TimeSpan.FromMinutes(2)) // Allow 1-minute interval + buffer
        {
            return Task.FromResult(HealthCheckResult.Degraded($"Reminder service last ran {elapsed.TotalSeconds:N0} seconds ago"));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Reminder checker is running and up to date"));
    }
}
