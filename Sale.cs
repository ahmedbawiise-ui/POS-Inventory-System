namespace POS.Domain.Entities;

public class Sale : BaseEntity
{
    public required string InvoiceNumber { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public required string PaymentMethod { get; set; }
    public int UserId { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
}