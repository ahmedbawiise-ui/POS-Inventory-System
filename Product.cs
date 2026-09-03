namespace POS.Domain.Entities;

public class Product : BaseEntity
{
    public required string Barcode { get; set; }
    public required string Name { get; set; }
    public int CategoryId { get; set; }
    public int SupplierId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal RetailPrice { get; set; }
    public int StockQuantity { get; set; }
    public int MinStockLevel { get; set; }
    public uint Version { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public virtual Category Category { get; set; } = null!;
    public virtual Supplier Supplier { get; set; } = null!;
    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    public virtual ICollection<StockAdjustmentLog> StockAdjustmentLogs { get; set; } = new List<StockAdjustmentLog>();
}