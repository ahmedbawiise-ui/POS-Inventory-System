namespace POS.Application.DTOs;

public class SaleListDto
{
    public int Id { get; set; }
    public required string InvoiceNumber { get; set; }
    public DateTime SaleDate { get; set; }
    public decimal TotalAmount { get; set; }
    public required string PaymentMethod { get; set; }
    public required string CashierUsername { get; set; }
    public required List<SaleLineItemDto> Items { get; set; }
}

public class SaleLineItemDto
{
    public required string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}