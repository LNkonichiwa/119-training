using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Core.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync();
    Task<IReadOnlyList<Product>> GetActiveAsync();
    Task<Product?> GetByIdAsync(int id);
    Task<IReadOnlyList<LowStockProduct>> GetLowStockAsync(int threshold, DateTime soldSince);
    Task SaveChangesAsync();
}
