namespace backend.Configuration;

public class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public GlobalLimiter GlobalLimiter { get; set; } = new();
    public ActionItemsLimiter ActionItems { get; set; } = new();
    public AiLimiter Ai { get; set; } = new();
    public AuthLimiter Auth { get; set; } = new();
}

public class GlobalLimiter
{
    public int AuthenticatedUserLimit { get; set; } = 200;
    public int AnonymousUserLimit { get; set; } = 50;
    public int AuthenticatedUserQueueLimit { get; set; } = 10;
    public int AnonymousUserQueueLimit { get; set; } = 2;
    public int WindowMinutes { get; set; } = 1;
}

public class ActionItemsLimiter
{
    public int AuthenticatedUserLimit { get; set; } = 300;
    public int AnonymousUserLimit { get; set; } = 30;
    public int AuthenticatedUserQueueLimit { get; set; } = 20;
    public int AnonymousUserQueueLimit { get; set; } = 5;
    public int WindowMinutes { get; set; } = 1;
}

public class AiLimiter
{
    public int AuthenticatedUserLimit { get; set; } = 20;
    public int AnonymousUserLimit { get; set; } = 5;
    public int AuthenticatedUserQueueLimit { get; set; } = 5;
    public int AnonymousUserQueueLimit { get; set; } = 1;
    public int WindowMinutes { get; set; } = 1;
}

public class AuthLimiter
{
    public int UserLimit { get; set; } = 10;
    public int QueueLimit { get; set; } = 2;
    public int WindowMinutes { get; set; } = 1;
}
