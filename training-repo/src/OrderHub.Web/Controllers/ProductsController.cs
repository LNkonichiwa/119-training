using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Services;
using OrderHub.Web.ViewModels;

namespace OrderHub.Web.Controllers;

public class ProductsController : Controller
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _productService.GetAllAsync();

        var vm = new ProductListViewModel
        {
            Products = products.Select(p => new ProductRowViewModel
            {
                Sku = p.Sku,
                Name = p.Name,
                UnitPrice = p.UnitPrice,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive
            }).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> LowStock(LowStockViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Products = Array.Empty<LowStockRowViewModel>();
            return View(vm);
        }

        var results = await _productService.GetLowStockAsync(vm.Threshold);

        vm.Products = results.Select(r => new LowStockRowViewModel
        {
            Sku = r.Sku,
            Name = r.Name,
            StockQuantity = r.StockQuantity,
            Sold30Days = r.Sold30Days
        }).ToList();

        return View(vm);
    }
}

