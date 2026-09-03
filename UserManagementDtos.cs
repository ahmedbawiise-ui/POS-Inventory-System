namespace POS.Application.DTOs;

public class UserListDto
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string RoleName { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserAdminDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
    public int RoleId { get; set; }
}

public class ResetPasswordDto
{
    public required string NewPassword { get; set; }
}