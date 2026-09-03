namespace POS.Domain.Entities;

public class SaleItem : BaseEntity
{
    public int SaleId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }

    // Historical snapshot of the product's price at the moment of checkout
    public decimal UnitPrice { get; set; }

    public virtual Sale Sale { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
