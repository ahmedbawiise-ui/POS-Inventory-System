namespace POS.Application.DTOs;

public class RestockRequestDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int PerformedByUserId { get; set; }
    public required string Reason { get; set; }
}

public class StockAdjustmentRequestDto
{
    public int ProductId { get; set; }
    public int QuantityChanged { get; set; }
    public required string AdjustmentType { get; set; } // "Restock", "Damage", "Audit", "Return"
    public required string Reason { get; set; }
    public int PerformedByUserId { get; set; }
}