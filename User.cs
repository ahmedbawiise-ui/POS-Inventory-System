namespace POS.Domain.Entities;

public class User : BaseEntity
{
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual Role Role { get; set; } = null!;
    public virtual ICollection<Sale> Sales { get; set; } = new List<Sale>();
}