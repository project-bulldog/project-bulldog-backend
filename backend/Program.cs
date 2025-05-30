using System.Text;
using System.Text.Json;
using backend.Data;
using backend.HealthChecks;
using backend.Infrastructure;
using backend.Services.Auth;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Database
builder.Services.AddDbContext<BulldogDbContext>(options =>
{
    var connString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connString);
});

//Authentication/Authorization
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });
builder.Services.AddAuthorization();

//Application Services
builder.Services.AddScoped<IActionItemService, ActionItemService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<ISummaryService, SummaryService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAiService, AiService>();
builder.Services.AddScoped<IOpenAiService, OpenAiService>();
builder.Services.AddScoped<IReminderProcessor, ReminderProcessor>();
builder.Services.AddHostedService<ReminderCheckerService>();
builder.Services.AddSingleton<ReminderServiceState>();
builder.Services.AddSingleton<INotificationService, FakeNotificationService>();



//Fluent Validation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

//Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BulldogDbContext>("Database")
    .AddCheck<ReminderHealthCheck>("ReminderChecker");


var app = builder.Build();

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
var logger = app.Services.GetRequiredService<ILogger<Program>>();


app.MapGet("/server-time", () => Results.Ok(DateTime.UtcNow));

//Exception Handling
app.UseExceptionHandler("/error");

//Security Middleware
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

//Swagger (in dev/prod)
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//Database Migration and Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<BulldogDbContext>();
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
