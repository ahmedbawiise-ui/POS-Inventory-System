using POS.Domain.Enums;

namespace POS.Domain.Entities;

public class StockAdjustmentLog : BaseEntity
{
    public int ProductId { get; set; }

    // Positive = stock added (restock, return). Negative = stock removed (damage, sale, theft).
    public int QuantityChanged { get; set; }

    public AdjustmentType AdjustmentType { get; set; }

    public required string Reason { get; set; }

    public int PerformedByUserId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public virtual Product Product { get; set; } = null!;
    public virtual User PerformedByUser { get; set; } = null!;
}