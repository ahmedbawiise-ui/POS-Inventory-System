using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/products — all authenticated roles
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var products = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.Barcode,
                p.Name,
                p.CategoryId,
                p.SupplierId,
                p.CostPrice,
                p.RetailPrice,
                p.StockQuantity,
                p.MinStockLevel,
                p.ExpiryDate,
                p.CreatedAt,
                Category = new { p.Category.Id, p.Category.Name },
                Supplier = new { p.Supplier.Id, p.Supplier.Name }
            })
            .ToListAsync();

        return Ok(products);
    }

    // GET: api/products/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Barcode,
                p.Name,
                p.CategoryId,
                p.SupplierId,
                p.CostPrice,
                p.RetailPrice,
                p.StockQuantity,
                p.MinStockLevel,
                p.ExpiryDate,
                p.CreatedAt,
                Category = new { p.Category.Id, p.Category.Name },
                Supplier = new { p.Supplier.Id, p.Supplier.Name }
            })
            .FirstOrDefaultAsync();

        if (product is null)
            return NotFound($"Product {id} not found.");

        return Ok(product);
    }

    // POST: api/products — Admin AND Salesman can create
    [HttpPost]
    [Authorize(Roles = "Admin,Salesman")]
    public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
    {
        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == dto.CategoryId);
        if (!categoryExists)
            return BadRequest($"CategoryId {dto.CategoryId} does not exist.");

        var supplierExists = await _context.Suppliers
            .AnyAsync(s => s.Id == dto.SupplierId);
        if (!supplierExists)
            return BadRequest($"SupplierId {dto.SupplierId} does not exist.");

        var product = new Product
        {
            Barcode = dto.Barcode,
            Name = dto.Name,
            CategoryId = dto.CategoryId,
            SupplierId = dto.SupplierId,
            CostPrice = dto.CostPrice,
            RetailPrice = dto.RetailPrice,
            StockQuantity = dto.StockQuantity,
            MinStockLevel = dto.MinStockLevel,
            ExpiryDate = dto.ExpiryDate
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    // PUT: api/products/5 — Admin AND Salesman can edit
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,Salesman")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductDto dto)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
            return NotFound($"Product {id} not found.");

        product.Name = dto.Name;
        product.Barcode = dto.Barcode;
        product.CategoryId = dto.CategoryId;
        product.SupplierId = dto.SupplierId;
        product.CostPrice = dto.CostPrice;
        product.RetailPrice = dto.RetailPrice;
        product.MinStockLevel = dto.MinStockLevel;
        product.ExpiryDate = dto.ExpiryDate;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/products/5 — Admin only
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product is null)
            return NotFound($"Product {id} not found.");

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}