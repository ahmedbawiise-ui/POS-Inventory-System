using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using POS.Application.DTOs;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.Infrastructure.Services;

public class PosService : IPosService
{
    private readonly ApplicationDbContext _context;
    private readonly IInventoryService _inventoryService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PosService> _logger;

    private static readonly string[] ValidPaymentMethods =
        { "Cash", "Card", "MobileMoney" };

    public PosService(
        ApplicationDbContext context,
        IInventoryService inventoryService,
        IConfiguration configuration,
        ILogger<PosService> logger)
    {
        _context = context;
        _inventoryService = inventoryService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ReceiptResultDto> CheckoutAsync(
        CheckoutRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException(
                "Cart must contain at least one item.", nameof(request));

        if (request.Items.Any(i => i.Quantity <= 0))
            throw new ArgumentException(
                "All item quantities must be greater than zero.");

        if (!ValidPaymentMethods.Contains(
                request.PaymentMethod,
                StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Invalid payment method. Must be one of: " +
                $"{string.Join(", ", ValidPaymentMethods)}");

        var taxRate =
            _configuration.GetValue<decimal?>("Billing:TaxRatePercentage")
            ?? 0m;

        // Use CreateExecutionStrategy to wrap the manual transaction.
        // This is required when EnableRetryOnFailure is configured.
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Merge duplicate product entries
                var mergedItems = request.Items
                    .GroupBy(i => i.ProductId)
                    .Select(g => new
                    {
                        ProductId = g.Key,
                        Quantity = g.Sum(x => x.Quantity)
                    })
                    .ToList();

                var productIds = mergedItems.Select(i => i.ProductId).ToList();

                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToListAsync(cancellationToken);

                if (products.Count != productIds.Count)
                {
                    var missing = productIds.Except(products.Select(p => p.Id));
                    await transaction.RollbackAsync(cancellationToken);
                    throw new InvalidOperationException(
                        $"One or more products not found: " +
                        $"{string.Join(", ", missing)}");
                }

                var cashier = await _context.Users
                    .FirstOrDefaultAsync(
                        u => u.Id == request.CashierUserId,
                        cancellationToken);

                if (cashier is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new InvalidOperationException(
                        $"Cashier User {request.CashierUserId} not found.");
                }

                // Snapshot prices and build line items
                var saleItems = new List<SaleItem>();
                var receiptLines = new List<ReceiptLineItemDto>();
                decimal subtotal = 0m;

                foreach (var item in mergedItems)
                {
                    var product = products.First(p => p.Id == item.ProductId);
                    var unitPrice = product.RetailPrice;
                    var lineTotal = unitPrice * item.Quantity;
                    subtotal += lineTotal;

                    saleItems.Add(new SaleItem
                    {
                        ProductId = product.Id,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice
                    });

                    receiptLines.Add(new ReceiptLineItemDto
                    {
                        ProductName = product.Name,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        LineTotal = lineTotal
                    });
                }

                var taxAmount = Math.Round(
                    subtotal * (taxRate / 100m),
                    2,
                    MidpointRounding.AwayFromZero);
                var grandTotal = subtotal + taxAmount;

                decimal? changeDue = null;
                if (string.Equals(
                        request.PaymentMethod,
                        "Cash",
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (request.AmountTendered is null ||
                        request.AmountTendered < grandTotal)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw new InvalidOperationException(
                            "Amount tendered is missing or insufficient " +
                            "to cover the total due.");
                    }
                    changeDue = request.AmountTendered.Value - grandTotal;
                }

                // Deduct stock — joins THIS transaction
                var quantitiesByProduct = mergedItems
                    .ToDictionary(i => i.ProductId, i => i.Quantity);

                var stockDeducted =
                    await _inventoryService.ProcessSaleStockDeductionAsync(
                        quantitiesByProduct,
                        request.CashierUserId,
                        cancellationToken);

                if (!stockDeducted)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new InvalidOperationException(
                        "Checkout failed: one or more items are out of stock.");
                }

                // Generate invoice number
                var invoiceNumber =
                    await GenerateInvoiceNumberAsync(cancellationToken);

                var sale = new Sale
                {
                    InvoiceNumber = invoiceNumber,
                    SaleDate = DateTime.UtcNow,
                    TotalAmount = grandTotal,
                    PaymentMethod = request.PaymentMethod,
                    UserId = request.CashierUserId,
                    SaleItems = saleItems
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Checkout completed. Invoice {InvoiceNumber}, " +
                    "Total {GrandTotal}.",
                    invoiceNumber, grandTotal);

                var receipt = new ReceiptResultDto
                {
                    SaleId = sale.Id,
                    InvoiceNumber = invoiceNumber,
                    SaleDate = sale.SaleDate,
                    CashierUsername = cashier.Username,
                    PaymentMethod = request.PaymentMethod,
                    Subtotal = subtotal,
                    TaxRate = taxRate,
                    TaxAmount = taxAmount,
                    GrandTotal = grandTotal,
                    AmountTendered = request.AmountTendered,
                    ChangeDue = changeDue,
                    LineItems = receiptLines,
                    RawReceiptHtml = string.Empty
                };

                receipt.RawReceiptHtml = BuildReceiptHtml(receipt);
                return receipt;
            }
            catch (Exception ex)
                when (ex is not InvalidOperationException
                      and not ArgumentException)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex,
                    "Unexpected error during checkout.");
                throw;
            }
        });
    }

    private async Task<string> GenerateInvoiceNumberAsync(
        CancellationToken cancellationToken)
    {
        var nextVal = await _context.Database
            .SqlQuery<long>(
                $"SELECT nextval('invoice_number_seq') AS \"Value\"")
            .FirstAsync(cancellationToken);

        return $"INV-{DateTime.UtcNow.Year}-{nextVal:D5}";
    }

    private static string BuildReceiptHtml(ReceiptResultDto receipt)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"width:80mm;font-family:'Courier New'," +
                  "monospace;font-size:12px;color:#000;\">");

        sb.Append("<div style=\"text-align:center;margin-bottom:6px;\">");
        sb.Append("<svg width=\"48\" height=\"48\" viewBox=\"0 0 24 24\" " +
                  "fill=\"none\" stroke=\"#000\" stroke-width=\"1.5\" " +
                  "stroke-linecap=\"round\" stroke-linejoin=\"round\">");
        sb.Append("<circle cx=\"9\" cy=\"21\" r=\"1\"></circle>");
        sb.Append("<circle cx=\"20\" cy=\"21\" r=\"1\"></circle>");
        sb.Append("<path d=\"M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72" +
                  "a2 2 0 0 0 2-1.61L23 6H6\"></path>");
        sb.Append("</svg>");
        sb.Append("</div>");

        sb.Append("<div style=\"text-align:center;font-weight:bold;" +
                  "font-size:15px;\">DAILY NEEDS VENTURES</div>");
        sb.Append("<div style=\"text-align:center;font-size:11px;\">" +
                  "POS &amp; Inventory Management</div>");
        sb.Append("<hr style=\"border-top:1px dashed #000;\"/>");
        sb.Append($"<div>Invoice: {receipt.InvoiceNumber}</div>");
        sb.Append($"<div>Date: {receipt.SaleDate:yyyy-MM-dd HH:mm}</div>");
        sb.Append($"<div>Cashier: {receipt.CashierUsername}</div>");
        sb.Append("<hr style=\"border-top:1px dashed #000;\"/>");

        sb.Append("<table style=\"width:100%;border-collapse:collapse;\">");
        foreach (var line in receipt.LineItems)
        {
            sb.Append("<tr><td colspan=\"2\">")
              .Append(line.ProductName)
              .Append("</td></tr>");
            sb.Append("<tr>");
            sb.Append($"<td>{line.Quantity} x {line.UnitPrice:N2}</td>");
            sb.Append($"<td style=\"text-align:right;\">" +
                      $"{line.LineTotal:N2}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");

        sb.Append("<hr style=\"border-top:1px dashed #000;\"/>");
        sb.Append("<table style=\"width:100%;\">");
        sb.Append($"<tr><td style=\"font-weight:bold;\">TOTAL</td>" +
                  $"<td style=\"text-align:right;font-weight:bold;\">" +
                  $"{receipt.GrandTotal:N2}</td></tr>");

        if (receipt.AmountTendered.HasValue)
        {
            sb.Append($"<tr><td>Tendered</td>" +
                      $"<td style=\"text-align:right;\">" +
                      $"{receipt.AmountTendered:N2}</td></tr>");
            sb.Append($"<tr><td>Change</td>" +
                      $"<td style=\"text-align:right;\">" +
                      $"{receipt.ChangeDue:N2}</td></tr>");
        }
        sb.Append("</table>");

        sb.Append("<hr style=\"border-top:1px dashed #000;\"/>");
        sb.Append($"<div>Payment: {receipt.PaymentMethod}</div>");
        sb.Append("<div style=\"text-align:center;margin-top:8px;\">" +
                  "Thank you for shopping with us!</div>");
        sb.Append("</div>");

        return sb.ToString();
    }
}