using System.ComponentModel.DataAnnotations;

namespace OrderHub.Web.ViewModels;

public class ProductListViewModel
{
    public IReadOnlyList<ProductRowViewModel> Products { get; set; } = Array.Empty<ProductRowViewModel>();
}

public class ProductRowViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }
}

public class LowStockViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "門檻必須是大於 0 的整數")]
    [Display(Name = "庫存門檻")]
    public int Threshold { get; set; } = 10;

    public IReadOnlyList<LowStockRowViewModel> Products { get; set; } = Array.Empty<LowStockRowViewModel>();
}

public class LowStockRowViewModel
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public int Sold30Days { get; set; }
}
