var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "Hello world!");
app.MapGet("/weather", () =>
{
    return new[] {
        new { Date = DateTime.Now, TempC = 25, Summary = "Sunny" }
    };
});

app.Run();
