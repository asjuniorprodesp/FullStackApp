using System.Text.Json;

namespace ClientApp.Services;

public sealed class ProductCatalogService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient httpClient;
    private readonly SemaphoreSlim cacheLock = new(1, 1);

    private Product[]? cachedProducts;
    private DateTimeOffset cacheExpiresAt;

    public ProductCatalogService(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<Product[]> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        if (IsCacheValid())
        {
            return cachedProducts!;
        }

        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (IsCacheValid())
            {
                return cachedProducts!;
            }

            // Fetch only when the cached snapshot expired.
            using var response = await httpClient.GetAsync("/api/productlist", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            cachedProducts = JsonSerializer.Deserialize<Product[]>(json, JsonOptions) ?? Array.Empty<Product>();
            cacheExpiresAt = DateTimeOffset.UtcNow.Add(CacheDuration);
            return cachedProducts;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    private bool IsCacheValid()
    {
        return cachedProducts is not null && DateTimeOffset.UtcNow < cacheExpiresAt;
    }

    public sealed class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public Category Category { get; set; } = new();
    }

    public sealed class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
