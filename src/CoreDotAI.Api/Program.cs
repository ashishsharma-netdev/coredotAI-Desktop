var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5080");
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "CoreDotAI API",
    version = "0.1.0",
    status = "running"
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestampUtc = DateTimeOffset.UtcNow
}));

app.Run();
