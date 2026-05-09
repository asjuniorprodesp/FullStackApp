using Microsoft.Extensions.Caching.Memory;
using ServerApp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();
builder.Services.AddMemoryCache();

var app = builder.Build();

app.UseCors(policy =>
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader());

app.MapGet("/api/productlist", async (IMemoryCache cache) =>
{
    // Keep the static catalog in memory to avoid rebuilding the payload every request.
    return await cache.GetOrCreateAsync("productlist", entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        return Task.FromResult(ProductCatalog.Products);
    });
});

app.Run();