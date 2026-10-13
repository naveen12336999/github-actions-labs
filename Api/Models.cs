using System.ComponentModel.DataAnnotations;
namespace Shop.Api;

public enum OrderStatus { Pending, Paid, Shipped, Delivered, Cancelled }

public class Product
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(1000)] public string? Description { get; set; }
    [Range(0.01, 1_000_000)] public decimal Price { get; set; }
    [Range(0, int.MaxValue)] public int Stock { get; set; }
    [MaxLength(500)] public string? ImageUrl { get; set; }
    [MaxLength(60)] public string Category { get; set; } = "General";
    public double Rating { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [Required, MaxLength(200)] public string CustomerEmail { get; set; } = "";
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public List<OrderLine> Lines { get; set; } = new();
}

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public record ProductDto(int Id, string Name, string? Description, decimal Price, int Stock, string? ImageUrl, string Category, double Rating);
public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public record ProductInput(
    [property: Required, MaxLength(120)] string Name,
    [property: MaxLength(1000)] string? Description,
    [property: Range(0.01, 1_000_000)] decimal Price,
    [property: Range(0, int.MaxValue)] int Stock,
    [property: MaxLength(500)] string? ImageUrl,
    [property: Required, MaxLength(60)] string Category);

public record OrderLineInput([property: Range(1, int.MaxValue)] int ProductId, [property: Range(1, 100)] int Quantity);
public record OrderInput(
    [property: Required, EmailAddress, MaxLength(200)] string CustomerEmail,
    [property: Required, MinLength(1)] List<OrderLineInput> Lines);
public record StatusInput(OrderStatus Status);

public record OrderDto(int Id, DateTime CreatedUtc, string CustomerEmail, decimal Total, OrderStatus Status, IEnumerable<OrderLineDto> Lines);
public record OrderLineDto(int ProductId, string ProductName, int Quantity, decimal UnitPrice);
