using System.Threading.RateLimiting;
using backend.Configuration;
using backend.Helpers;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Extensions;

public static class RateLimitingExtensions
{
    /// <summary>
    /// Configures custom rate limiting for the application based on environment and configuration settings.
    /// Disables rate limiting in development mode and enables it with specific policies in production.
    /// </summary>
    public static void AddCustomRateLimiting(this IServiceCollection services, IWebHostEnvironment environment,
        IConfiguration configuration, ILogger logger)
    {
        // Configure options from appsettings
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            if (environment.IsDevelopment())
            {
                // In development, create policies but with no limits
                ConfigureGlobalLimiter(options, new GlobalLimiter());
                ConfigureActionItemsLimiter(options, new ActionItemsLimiter());
                ConfigureAiLimiter(options, new AiLimiter());
                ConfigureAuthLimiter(options, new AuthLimiter());
                ConfigureRejectionHandling(options, logger);

                logger.LogInformation("🚀 Rate limiting DISABLED in Development mode");
            }
            else
            {
                // Get configuration using the proper binding approach
                var rateLimitingOptions = new RateLimitingOptions();
                configuration.GetSection(RateLimitingOptions.SectionName).Bind(rateLimitingOptions);

                ConfigureGlobalLimiter(options, rateLimitingOptions.GlobalLimiter);
                ConfigureActionItemsLimiter(options, rateLimitingOptions.ActionItems);
                ConfigureAiLimiter(options, rateLimitingOptions.Ai);
                ConfigureAuthLimiter(options, rateLimitingOptions.Auth);
                ConfigureRejectionHandling(options, logger);

                logger.LogInformation("🛡️ Rate limiting ENABLED in Production mode");
            }
        });
    }

    /// <summary>
    /// Configures the global rate limiter with different limits for authenticated and anonymous users.
    /// Uses user ID for authenticated users and IP address for anonymous users as partition keys.
    /// </summary>
    private static void ConfigureGlobalLimiter(RateLimiterOptions options, GlobalLimiter config)
    {
        if (IsGlobalLimiterDisabled(config))
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create(CreateNoLimiter());
            return;
        }
        options.GlobalLimiter = PartitionedRateLimiter.Create(CreateUserBasedRateLimiter(
            config.AuthenticatedUserLimit, config.AnonymousUserLimit,
            config.AuthenticatedUserQueueLimit, config.AnonymousUserQueueLimit, config.WindowMinutes));
    }

    /// <summary>
    /// Configures rate limiting specifically for action items endpoints with separate limits for authenticated and anonymous users.
    /// Creates a policy named "ActionItems" that can be applied to specific controllers or actions.
    /// </summary>
    private static void ConfigureActionItemsLimiter(RateLimiterOptions options, ActionItemsLimiter config)
    {
        if (IsActionItemsLimiterDisabled(config))
        {
            options.AddPolicy("ActionItems", CreateNoLimiter());
            return;
        }
        options.AddPolicy("ActionItems", CreateUserBasedRateLimiter(
            config.AuthenticatedUserLimit, config.AnonymousUserLimit,
            config.AuthenticatedUserQueueLimit, config.AnonymousUserQueueLimit, config.WindowMinutes));
    }

    /// <summary>
    /// Configures rate limiting specifically for AI-related endpoints with separate limits for authenticated and anonymous users.
    /// Creates a policy named "AI" that can be applied to specific controllers or actions handling AI-related operations.
    /// Uses user ID for authenticated users and IP address for anonymous users as partition keys.
    /// </summary>
    private static void ConfigureAiLimiter(RateLimiterOptions options, AiLimiter config)
    {
        if (IsAiLimiterDisabled(config))
        {
            options.AddPolicy("AI", CreateNoLimiter());
            return;
        }
        options.AddPolicy("AI", CreateUserBasedRateLimiter(
            config.AuthenticatedUserLimit, config.AnonymousUserLimit,
            config.AuthenticatedUserQueueLimit, config.AnonymousUserQueueLimit, config.WindowMinutes));
    }

    /// <summary>
    /// Configures rate limiting specifically for authentication endpoints.
    /// Creates a policy named "Auth" that applies to authentication-related endpoints.
    /// Uses IP address as the partition key since users are not authenticated during these operations.
    /// </summary>
    private static void ConfigureAuthLimiter(RateLimiterOptions options, AuthLimiter config)
    {
        if (IsAuthLimiterDisabled(config))
        {
            options.AddPolicy("Auth", CreateNoLimiter());
            return;
        }
        options.AddPolicy("Auth", CreateIpBasedRateLimiter(
            config.UserLimit, config.QueueLimit, config.WindowMinutes));
    }

    /// <summary>
    /// Creates a user-based rate limiter that differentiates between authenticated and anonymous users.
    /// Uses user ID for authenticated users and IP address for anonymous users as partition keys.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateUserBasedRateLimiter(
        int authenticatedUserLimit, int anonymousUserLimit,
        int authenticatedUserQueueLimit, int anonymousUserQueueLimit,
        int windowMinutes)
    {
        return httpContext =>
        {
            var partitionKey = httpContext.User?.Identity?.IsAuthenticated == true
                ? httpContext.User?.FindFirst("sub")?.Value ?? httpContext.User?.FindFirst("nameid")?.Value
                : httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var isAuthenticated = httpContext.User?.Identity?.IsAuthenticated == true;
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = isAuthenticated ? authenticatedUserLimit : anonymousUserLimit,
                    Window = TimeSpan.FromMinutes(windowMinutes),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = isAuthenticated ? authenticatedUserQueueLimit : anonymousUserQueueLimit
                });
        };
    }

    /// <summary>
    /// Creates an IP-based rate limiter for endpoints where user authentication is not available.
    /// Uses IP address as the partition key.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateIpBasedRateLimiter(
        int userLimit, int queueLimit, int windowMinutes)
    {
        return httpContext =>
        {
            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = userLimit,
                    Window = TimeSpan.FromMinutes(windowMinutes),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = queueLimit
                });
        };
    }

    /// <summary>
    /// Creates a no-limiter partition for development mode or when rate limiting is disabled.
    /// </summary>
    private static Func<HttpContext, RateLimitPartition<string>> CreateNoLimiter()
    {
        return httpContext => RateLimitPartition.GetNoLimiter(httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

    /// <summary>
    /// Configures how rate limit rejections are handled, including the HTTP status code and logging.
    /// Sets up rejection handling to return HTTP 429 (Too Many Requests) and logs warning messages
    /// when rate limits are exceeded, including the request path and client IP address.
    /// </summary>
    private static void ConfigureRejectionHandling(RateLimiterOptions options, ILogger logger)
    {
        options.RejectionStatusCode = 429; // HTTP 429 Too Many Requests

        // Add logging for rate limit rejections
        options.OnRejected = (context, token) =>
        {
            var requestLogger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            requestLogger.LogWarning("Rate limit exceeded for {Path} from {IP}",
                LogSanitizer.SanitizeForLog(context.HttpContext.Request.Path.ToString()),
                context.HttpContext.Connection.RemoteIpAddress);
            return ValueTask.CompletedTask;
        };
    }

    // --- Helper methods for checking if a limiter is disabled ---
    private static bool IsGlobalLimiterDisabled(GlobalLimiter config)
    {
        return config.AuthenticatedUserLimit == 0 && config.AnonymousUserLimit == 0;
    }
    private static bool IsActionItemsLimiterDisabled(ActionItemsLimiter config)
    {
        return config.AuthenticatedUserLimit == 0 && config.AnonymousUserLimit == 0;
    }
    private static bool IsAiLimiterDisabled(AiLimiter config)
    {
        return config.AuthenticatedUserLimit == 0 && config.AnonymousUserLimit == 0;
    }
    private static bool IsAuthLimiterDisabled(AuthLimiter config)
    {
        return config.UserLimit == 0;
    }
}
