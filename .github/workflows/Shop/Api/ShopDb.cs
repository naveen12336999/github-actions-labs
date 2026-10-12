using Microsoft.EntityFrameworkCore;
namespace Shop.Api;

public class ShopDb(DbContextOptions<ShopDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        b.Entity<Product>().HasIndex(p => p.Name);
        b.Entity<Order>().Property(o => o.Total).HasPrecision(18, 2);
        b.Entity<OrderLine>().Property(l => l.UnitPrice).HasPrecision(18, 2);
        b.Entity<Product>().HasData(
            new Product { Id = 1, Name = "Aurora Headphones", Description = "Studio-grade wireless sound.", Price = 249.99m, Stock = 25, ImageUrl = "🎧" },
            new Product { Id = 2, Name = "Nebula Watch", Description = "Sapphire glass smartwatch.", Price = 399m, Stock = 12, ImageUrl = "⌚" },
            new Product { Id = 3, Name = "Orbit Speaker", Description = "360° room-filling audio.", Price = 129.5m, Stock = 40, ImageUrl = "🔊" },
            new Product { Id = 4, Name = "Prism Camera", Description = "Mirrorless 4K creator camera.", Price = 1199m, Stock = 6, ImageUrl = "📷" });
    }
}
