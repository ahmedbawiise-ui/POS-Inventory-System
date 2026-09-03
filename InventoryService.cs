using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<InventoryService> _logger;
    private const int MaxConcurrencyRetries = 3;

    public InventoryService(
        ApplicationDbContext context,
        ILogger<InventoryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> DeductStockAsync(
        int productId,
        int quantity,
        int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new ArgumentException(
                "Quantity to deduct must be greater than zero.",
                nameof(quantity));

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            for (int attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(
                            p => p.Id == productId, cancellationToken);

                    if (product is null)
                    {
                        _logger.LogWarning(
                            "DeductStockAsync: Product {ProductId} not found.",
                            productId);
                        return false;
                    }

                    if (product.StockQuantity < quantity)
                    {
                        _logger.LogWarning(
                            "DeductStockAsync: Insufficient stock for " +
                            "Product {ProductId}. Requested {Requested}, " +
                            "Available {Available}.",
                            productId, quantity, product.StockQuantity);
                        return false;
                    }

                    product.StockQuantity -= quantity;

                    _context.StockAdjustmentLogs.Add(new StockAdjustmentLog
                    {
                        ProductId = productId,
                        QuantityChanged = -quantity,
                        AdjustmentType = AdjustmentType.SaleDeduction,
                        Reason = "Stock deducted for sale",
                        PerformedByUserId = performedByUserId,
                        Timestamp = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync(cancellationToken);
                    return true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex,
                        "Concurrency conflict deducting stock for " +
                        "Product {ProductId}, attempt {Attempt}/{Max}.",
                        productId, attempt, MaxConcurrencyRetries);

                    foreach (var entry in _context.ChangeTracker
                                 .Entries().ToList())
                        entry.State = EntityState.Detached;

                    if (attempt == MaxConcurrencyRetries)
                    {
                        _logger.LogError(
                            "DeductStockAsync: Max retries exceeded " +
                            "for Product {ProductId}.", productId);
                        throw;
                    }
                }
            }

            return false;
        });
    }

    public async Task<bool> RestockProductAsync(
        int productId,
        int quantity,
        int performedByUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
            throw new ArgumentException(
                "Restock quantity must be greater than zero.",
                nameof(quantity));

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            for (int attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
            {
                try
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(
                            p => p.Id == productId, cancellationToken);

                    if (product is null)
                    {
                        _logger.LogWarning(
                            "RestockProductAsync: Product {ProductId} not found.",
                            productId);
                        return false;
                    }

                    product.StockQuantity += quantity;

                    _context.StockAdjustmentLogs.Add(new StockAdjustmentLog
                    {
                        ProductId = productId,
                        QuantityChanged = quantity,
                        AdjustmentType = AdjustmentType.Restock,
                        Reason = reason,
                        PerformedByUserId = performedByUserId,
                        Timestamp = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation(
                        "Restocked Product {ProductId} by {Quantity}.",
                        productId, quantity);
                    return true;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex,
                        "Concurrency conflict restocking Product {ProductId}, " +
                        "attempt {Attempt}/{Max}.",
                        productId, attempt, MaxConcurrencyRetries);

                    foreach (var entry in _context.ChangeTracker
                                 .Entries().ToList())
                        entry.State = EntityState.Detached;

                    if (attempt == MaxConcurrencyRetries)
                        throw;
                }
            }

            return false;
        });
    }

    public async Task LogAdjustmentAsync(
        int productId,
        int quantityChanged,
        AdjustmentType adjustmentType,
        string reason,
        int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (quantityChanged == 0)
            throw new ArgumentException(
                "Quantity changed cannot be zero for an adjustment.",
                nameof(quantityChanged));

        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(
                        p => p.Id == productId, cancellationToken);

                if (product is null)
                    throw new InvalidOperationException(
                        $"Product {productId} not found.");

                var newStock = product.StockQuantity + quantityChanged;
                if (newStock < 0)
                    throw new InvalidOperationException(
                        $"Adjustment would result in negative stock for " +
                        $"Product {productId} (current: " +
                        $"{product.StockQuantity}, change: {quantityChanged}).");

                product.StockQuantity = newStock;

                _context.StockAdjustmentLogs.Add(new StockAdjustmentLog
                {
                    ProductId = productId,
                    QuantityChanged = quantityChanged,
                    AdjustmentType = adjustmentType,
                    Reason = reason,
                    PerformedByUserId = performedByUserId,
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Logged {AdjustmentType} adjustment of {Quantity} " +
                    "for Product {ProductId} by User {UserId}.",
                    adjustmentType, quantityChanged, productId, performedByUserId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex,
                    "Failed to log adjustment for Product {ProductId}. " +
                    "Transaction rolled back.", productId);
                throw;
            }
        });
    }

    public async Task<List<Product>> GetLowStockProductsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .Where(p => p.StockQuantity <= p.MinStockLevel)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ProcessSaleStockDeductionAsync(
        Dictionary<int, int> productQuantities,
        int performedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (productQuantities is null || productQuantities.Count == 0)
            throw new ArgumentException(
                "At least one product must be provided.",
                nameof(productQuantities));

        // Join the existing transaction started by PosService if present.
        // If no transaction exists, start our own inside the execution strategy.
        var existingTransaction = _context.Database.CurrentTransaction;

        if (existingTransaction is not null)
        {
            // Already inside PosService's strategy.ExecuteAsync — just run directly
            return await RunStockDeductionAsync(
                productQuantities, performedByUserId,
                existingTransaction, ownsTransaction: false,
                cancellationToken);
        }

        // No outer transaction — wrap in our own strategy
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            return await RunStockDeductionAsync(
                productQuantities, performedByUserId,
                transaction, ownsTransaction: true,
                cancellationToken);
        });
    }

    private async Task<bool> RunStockDeductionAsync(
        Dictionary<int, int> productQuantities,
        int performedByUserId,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        bool ownsTransaction,
        CancellationToken cancellationToken)
    {
        try
        {
            var productIds = productQuantities.Keys.ToList();

            var products = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            if (products.Count != productIds.Count)
            {
                var missingIds = productIds.Except(products.Select(p => p.Id));
                _logger.LogWarning(
                    "ProcessSaleStockDeductionAsync: Missing products {MissingIds}.",
                    string.Join(",", missingIds));
                if (ownsTransaction)
                    await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            // Validate all stock levels before deducting anything
            foreach (var product in products)
            {
                var requestedQty = productQuantities[product.Id];
                if (product.StockQuantity < requestedQty)
                {
                    _logger.LogWarning(
                        "ProcessSaleStockDeductionAsync: Insufficient stock " +
                        "for Product {ProductId}. Requested {Requested}, " +
                        "Available {Available}.",
                        product.Id, requestedQty, product.StockQuantity);
                    if (ownsTransaction)
                        await transaction.RollbackAsync(cancellationToken);
                    return false;
                }
            }

            // All validated — apply deductions
            foreach (var product in products)
            {
                var requestedQty = productQuantities[product.Id];
                product.StockQuantity -= requestedQty;

                _context.StockAdjustmentLogs.Add(new StockAdjustmentLog
                {
                    ProductId = product.Id,
                    QuantityChanged = -requestedQty,
                    AdjustmentType = AdjustmentType.SaleDeduction,
                    Reason = "Stock deducted for sale (multi-item transaction)",
                    PerformedByUserId = performedByUserId,
                    Timestamp = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (ownsTransaction)
                await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Sale stock deduction committed for {Count} product(s).",
                products.Count);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            if (ownsTransaction)
                await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex,
                "Concurrency conflict during multi-item sale deduction.");
            return false;
        }
        catch (Exception ex)
        {
            if (ownsTransaction)
                await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex,
                "Unexpected error during sale stock deduction.");
            throw;
        }
    }
}