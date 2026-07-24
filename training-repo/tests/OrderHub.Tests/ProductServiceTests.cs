using OrderHub.Core.Domain;

namespace OrderHub.Tests;

public class ProductServiceTests
{
    [Fact]
    public async Task GetAll_ReturnsAllProductsIncludingInactive()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetAllAsync();

        Assert.Equal(2, products.Count);
    }

    [Fact]
    public async Task GetActive_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-A001");
        TestSetup.AddProduct(db, sku: "SKU-A002", isActive: false);

        var products = await service.GetActiveAsync();

        Assert.All(products, p => Assert.True(p.IsActive));
        Assert.Single(products);
    }

    [Fact]
    public async Task GetLowStock_FiltersByThresholdAndOrdersByStockAscending()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-LOW-1", stock: 8);
        TestSetup.AddProduct(db, sku: "SKU-LOW-2", stock: 3);
        TestSetup.AddProduct(db, sku: "SKU-HIGH", stock: 15);
        TestSetup.AddProduct(db, sku: "SKU-EDGE", stock: 10);

        var results = await service.GetLowStockAsync(10);

        Assert.Equal(new[] { "SKU-LOW-2", "SKU-LOW-1" }, results.Select(r => r.Sku));
    }

    [Fact]
    public async Task GetLowStock_ExcludesInactiveProducts()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        TestSetup.AddProduct(db, sku: "SKU-INACTIVE", stock: 1, isActive: false);
        TestSetup.AddProduct(db, sku: "SKU-ACTIVE", stock: 1, isActive: true);

        var results = await service.GetLowStockAsync(10);

        Assert.Single(results);
        Assert.Equal("SKU-ACTIVE", results[0].Sku);
    }

    [Fact]
    public async Task GetLowStock_Sold30Days_ExcludesCancelledOrders()
    {
        using var db = TestSetup.CreateContext();
        var service = TestSetup.CreateProductService(db);
        var customer = TestSetup.AddCustomer(db);
        var product = TestSetup.AddProduct(db, sku: "SKU-SOLD", stock: 2);

        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            Items = { new OrderItem { ProductId = product.Id, Quantity = 3, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.Orders.Add(new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Cancelled,
            CreatedAt = DateTime.UtcNow,
            Items = { new OrderItem { ProductId = product.Id, Quantity = 100, UnitPriceSnapshot = product.UnitPrice } }
        });
        db.SaveChanges();

        var results = await service.GetLowStockAsync(10);

        Assert.Equal(3, results.Single(r => r.Sku == "SKU-SOLD").Sold30Days);
    }
}
