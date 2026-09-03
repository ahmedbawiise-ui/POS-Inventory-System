using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Domain.Entities;
using POS.Infrastructure.Data;

namespace POS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<List<UserListDto>>> GetAll()
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .Select(u => new UserListDto
            {
                Id = u.Id,
                Username = u.Username,
                RoleName = u.Role.Name,
                RoleId = u.RoleId,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // POST: api/users
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateUserAdminDto dto)
    {
        var usernameExists = await _context.Users.AnyAsync(u => u.Username == dto.Username);
        if (usernameExists)
            return BadRequest("Username already exists.");

        var roleExists = await _context.Roles.AnyAsync(r => r.Id == dto.RoleId);
        if (!roleExists)
            return BadRequest("Invalid RoleId.");

        var user = new User
        {
            Username = dto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            RoleId = dto.RoleId,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User created successfully." });
    }

    // PUT: api/users/5/toggle-active
    [HttpPut("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (id == currentUserId)
            return BadRequest("You cannot deactivate your own account.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound($"User {id} not found.");

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new { message = user.IsActive ? "User activated." : "User deactivated.", isActive = user.IsActive });
    }

    // PUT: api/users/5/reset-password
    [HttpPut("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
            return BadRequest("Password must be at least 6 characters.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound($"User {id} not found.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password reset successfully." });
    }

    // DELETE: api/users/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (id == currentUserId)
            return BadRequest("You cannot delete your own account.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
            return NotFound($"User {id} not found.");

        var hasSales = await _context.Sales.AnyAsync(s => s.UserId == id);
        if (hasSales)
            return BadRequest("Cannot delete this user — they have sales history. Deactivate instead.");

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User deleted." });
    }
}