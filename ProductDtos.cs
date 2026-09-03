namespace POS.Application.DTOs;

public class CreateProductDto
{
    public required string Barcode { get; set; }
    public required string Name { get; set; }
    public int CategoryId { get; set; }
    public int SupplierId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal RetailPrice { get; set; }
    public int StockQuantity { get; set; }
    public int MinStockLevel { get; set; }
    public DateTime? ExpiryDate { get; set; }
}

public class UpdateProductDto
{
    public required string Barcode { get; set; }
    public required string Name { get; set; }
    public int CategoryId { get; set; }
    public int SupplierId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal RetailPrice { get; set; }
    public int MinStockLevel { get; set; }
    public DateTime? ExpiryDate { get; set; }
}