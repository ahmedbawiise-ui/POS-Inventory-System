namespace POS.Domain.Entities;

public class Supplier : BaseEntity
{
    public required string Name { get; set; }
    public string? ContactPhone { get; set; }
    public string? Email { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
