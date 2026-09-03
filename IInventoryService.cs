using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.Interfaces;

public interface IInventoryService
{
    Task<bool> DeductStockAsync(int productId, int quantity, int performedByUserId, CancellationToken cancellationToken = default);

    Task<bool> RestockProductAsync(int productId, int quantity, int performedByUserId, string reason, CancellationToken cancellationToken = default);

    Task LogAdjustmentAsync(int productId, int quantityChanged, AdjustmentType adjustmentType, string reason, int performedByUserId, CancellationToken cancellationToken = default);

    Task<List<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);

    Task<bool> ProcessSaleStockDeductionAsync(Dictionary<int, int> productQuantities, int performedByUserId, CancellationToken cancellationToken = default);
}