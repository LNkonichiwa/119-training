namespace OrderHub.Core.Services;

public class LowStockProduct
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int Sold30Days { get; set; }
}
