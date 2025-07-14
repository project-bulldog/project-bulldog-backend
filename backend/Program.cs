using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmail;
using Azure.Storage.Blobs;
using backend.Data;
using backend.Extensions;
using backend.HealthChecks;
using backend.Infrastructure;
using backend.Services.Auth.Implementations;
using backend.Services.Auth.Interfaces;
using backend.Services.FileUpload.Implementations;
using backend.Services.FileUpload.Interfaces;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

//Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddApplicationInsights();
builder.Logging.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information);

//Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});


//Configuration
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddUserSecrets("b701df2d-f6c0-4b66-be4e-7155271409ed")
    .AddEnvironmentVariables();

//Framework Services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        );
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
});

//Database
var env = builder.Environment.EnvironmentName;

var connectionName = env switch
{
    "Development" => "DevConnection",
    "QA" => "QaConnection",
    "Production" => "ProdConnection",
    _ => "DefaultConnection"
};

var connString = builder.Configuration.GetConnectionString(connectionName)
    ?? throw new Exception($"Missing connection string for environment: {env}");

// Database
builder.Services.AddDbContext<BulldogDbContext>(options =>
{
    options.UseNpgsql(connString);
});

//Authentication/Authorization
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddDataProtection();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ICookieService, CookieService>();
builder.Services.AddScoped<ICurrentUserProvider, CurrentUserProvider>();
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();

//Application Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ITokenCleanupService, TokenCleanupService>();
builder.Services.AddScoped<IActionItemService, ActionItemService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<ISummaryService, SummaryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<IOpenAiService, OpenAiService>();
builder.Services.AddScoped<IReminderProcessor, ReminderProcessor>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

//AWS Services
builder.Services.AddSingleton<IAmazonSimpleEmailService>(sp =>
{
    var accessKey = builder.Configuration["AWS:Credentials:AccessKey"];
    var secretKey = builder.Configuration["AWS:Credentials:SecretKey"];

    if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
    {
        throw new InvalidOperationException("AWS credentials not found in configuration. Ensure AWS:Credentials:AccessKey and AWS:Credentials:SecretKey are set in user secrets or environment variables.");
    }

    var config = new AmazonSimpleEmailServiceConfig { RegionEndpoint = RegionEndpoint.USEast2 };
    var credentials = new BasicAWSCredentials(accessKey, secretKey);

    return new AmazonSimpleEmailServiceClient(credentials, config);
});


//Background Services
builder.Services.AddHostedService<TokenCleanupHostedService>();
builder.Services.AddHostedService<ReminderCheckerService>();
builder.Services.AddHostedService<DstReminderAdjustmentService>();
builder.Services.AddSingleton<ReminderServiceState>();
builder.Services.AddScoped<INotificationService, SesNotificationService>();

//BlobStorage
builder.Services.AddSingleton(x =>
{
    var config = x.GetRequiredService<IConfiguration>();
    var connectionString = config.GetValue<string>("BlobConnectionString");
    return new BlobServiceClient(connectionString);
});

//Fluent Validation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

//Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BulldogDbContext>("Database")
    .AddCheck<ReminderHealthCheck>("ReminderChecker");

//Rate Limiting - Organized in extension method
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var rateLimitLogger = loggerFactory.CreateLogger<Program>();
builder.Services.AddCustomRateLimiting(builder.Environment, builder.Configuration, rateLimitLogger);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:3000",
            "https://project-bulldog-frontend-git-qa-calatheazs-projects.vercel.app",
            "https://project-bulldog-frontend-git-uat-calatheazs-projects.vercel.app",
            "https://project-bulldog-frontend-git-main-calatheazs-projects.vercel.app"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});


WebApplication app = builder.Build();

//Health Checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            }),
            totalDuration = report.TotalDuration
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

//Test logging
ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
app.MapGet("/server-time", () => Results.Ok(DateTime.UtcNow));


//Exception Handling
app.UseExceptionHandler("/error");

//Security Middleware
app.UseCors("AllowFrontend");
logger.LogInformation("✅ CORS policy 'AllowFrontend' applied.");
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

//Swagger (in dev/prod)
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//Database Migration and Seeding
using (IServiceScope scope = app.Services.CreateScope())
{
    IServiceProvider services = scope.ServiceProvider;
    try
    {
        BulldogDbContext db = services.GetRequiredService<BulldogDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error seeding database: {ex.Message}");
    }
}

//Endpoint Routing
app.MapControllers();

app.Run();
