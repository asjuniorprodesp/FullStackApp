namespace ServerApp;

public static class ProductCatalog
{
    // Static sample data keeps the endpoint small and easy to maintain.
    public static readonly ProductResponse[] Products =
    [
        new ProductResponse(
            1,
            "Laptop",
            1200.50m,
            25,
            new CategoryResponse(101, "Electronics")),
        new ProductResponse(
            2,
            "Headphones",
            50.00m,
            100,
            new CategoryResponse(102, "Accessories"))
    ];
}

public record ProductResponse(int Id, string Name, decimal Price, int Stock, CategoryResponse Category);

public record CategoryResponse(int Id, string Name);
