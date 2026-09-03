using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.DTOs;
using POS.Application.Interfaces;
using POS.Domain.Enums;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    // GET: api/inventory/low-stock — all authenticated roles
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStockProducts(CancellationToken cancellationToken)
    {
        var products = await _inventoryService.GetLowStockProductsAsync(cancellationToken);
        return Ok(products);
    }

    // POST: api/inventory/restock — Admin only
    [HttpPost("restock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Restock(
        [FromBody] RestockRequestDto request,
        CancellationToken cancellationToken)
    {
        var success = await _inventoryService.RestockProductAsync(
            request.ProductId, request.Quantity,
            request.PerformedByUserId, request.Reason, cancellationToken);

        if (!success)
            return NotFound($"Product {request.ProductId} not found.");

        return Ok(new { message = "Restock successful." });
    }

    // POST: api/inventory/adjustment — Admin only
    [HttpPost("adjustment")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> LogAdjustment(
        [FromBody] StockAdjustmentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AdjustmentType>(request.AdjustmentType, ignoreCase: true, out var adjustmentType))
            return BadRequest($"Invalid adjustment type: {request.AdjustmentType}.");

        try
        {
            await _inventoryService.LogAdjustmentAsync(
                request.ProductId, request.QuantityChanged,
                adjustmentType, request.Reason,
                request.PerformedByUserId, cancellationToken);

            return Ok(new { message = "Adjustment logged successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}