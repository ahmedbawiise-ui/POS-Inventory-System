namespace POS.Application.DTOs;

public class ReceiptResultDto
{
    public int SaleId { get; set; }
    public required string InvoiceNumber { get; set; }
    public DateTime SaleDate { get; set; }
    public required string CashierUsername { get; set; }
    public required string PaymentMethod { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal GrandTotal { get; set; }

    public decimal? AmountTendered { get; set; }
    public decimal? ChangeDue { get; set; }

    public required List<ReceiptLineItemDto> LineItems { get; set; }

    // Ready-to-render 80mm thermal receipt layout
    public required string RawReceiptHtml { get; set; }
}

public class ReceiptLineItemDto
{
    public required string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}