namespace POS.Application.DTOs;

public class CheckoutRequestDto
{
    public required List<CheckoutItemDto> Items { get; set; }

    // "Cash", "Card", or "MobileMoney"
    public required string PaymentMethod { get; set; }

    public int CashierUserId { get; set; }

    // Required only when PaymentMethod is "Cash"
    public decimal? AmountTendered { get; set; }
}

public class CheckoutItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}