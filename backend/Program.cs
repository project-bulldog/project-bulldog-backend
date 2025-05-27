using backend.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//Add configuration from appsettings + secrets
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddUserSecrets("b701df2d-f6c0-4b66-be4e-7155271409ed")
    .AddEnvironmentVariables();

//Add EF Core + PostgreSQL support
builder.Services.AddDbContext<BulldogDbContext>(options =>
{
    var connString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connString);
});

//Add Swagger and endpoint support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

var app = builder.Build();

// ✅ Run seed logic
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

app.UseExceptionHandler("/error"); // global exception handler

// Swagger in dev/prod
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();
app.Run();
