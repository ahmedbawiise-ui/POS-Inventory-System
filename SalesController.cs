using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Interfaces;
using POS.Infrastructure.Data;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly IPosService _posService;
    private readonly ApplicationDbContext _context;

    public SalesController(IPosService posService, ApplicationDbContext context)
    {
        _posService = posService;
        _context = context;
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await _posService.CheckoutAsync(request, cancellationToken);
            return Ok(receipt);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(ex.Message); }
    }

    // GET: api/sales?date=2026-07-18&range=week|month|year|all&invoice=INV-2026-00001
    [HttpGet]
    [Authorize(Roles = "Admin,Salesman")]
    public async Task<ActionResult<List<SaleListDto>>> GetAll(
        [FromQuery] string? date = null,
        [FromQuery] string? range = null,
        [FromQuery] string? invoice = null)
    {
        var query = _context.Sales
            .Include(s => s.User)
            .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
            .AsNoTracking();

        var isAdmin = User.IsInRole("Admin");
        var utcNow = DateTime.UtcNow;

        if (!isAdmin)
        {
            // Salesman always sees today only
            var todayUtc = utcNow.Date;
            query = query.Where(s => s.SaleDate.Date == todayUtc);
        }
        else if (!string.IsNullOrWhiteSpace(invoice))
        {
            // Invoice search takes priority
            query = query.Where(s => s.InvoiceNumber.ToLower().Contains(invoice.ToLower()));
        }
        else if (!string.IsNullOrWhiteSpace(range))
        {
            // Range filter
            query = range switch
            {
                "week" => query.Where(s => s.SaleDate >= utcNow.Date.AddDays(-6)),
                "month" => query.Where(s => s.SaleDate.Year == utcNow.Year && s.SaleDate.Month == utcNow.Month),
                "year" => query.Where(s => s.SaleDate.Year == utcNow.Year),
                "all" => query,
                _ => query.Where(s => s.SaleDate.Date == utcNow.Date)
            };
        }
        else if (!string.IsNullOrWhiteSpace(date) && DateTime.TryParse(date, out var parsedDate))
        {
            var targetDate = parsedDate.Date;
            query = query.Where(s => s.SaleDate.Date == targetDate);
        }
        else
        {
            // Default for Admin with no filter: today
            query = query.Where(s => s.SaleDate.Date == utcNow.Date);
        }

        var sales = await query
            .OrderByDescending(s => s.SaleDate)
            .Select(s => new SaleListDto
            {
                Id = s.Id,
                InvoiceNumber = s.InvoiceNumber,
                SaleDate = s.SaleDate,
                TotalAmount = s.TotalAmount,
                PaymentMethod = s.PaymentMethod,
                CashierUsername = s.User.Username,
                Items = s.SaleItems.Select(si => new SaleLineItemDto
                {
                    ProductName = si.Product.Name,
                    Quantity = si.Quantity,
                    UnitPrice = si.UnitPrice
                }).ToList()
            })
            .ToListAsync();

        return Ok(sales);
    }
}