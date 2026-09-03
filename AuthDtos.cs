namespace POS.Application.DTOs;

public class LoginRequestDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class LoginResponseDto
{
    public required string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int UserId { get; set; }
    public required string Username { get; set; }
    public required string Role { get; set; }
}

public class RegisterUserRequestDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public int RoleId { get; set; }
}