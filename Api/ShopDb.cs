using Microsoft.EntityFrameworkCore;
namespace Shop.Api;

public class ShopDb(DbContextOptions<ShopDb> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    private static readonly (string N, string D, decimal P, int S, string I, string C, double R)[] Seed =
    [
        ("Aurora Headphones", "Studio-grade wireless sound.", 249.99m, 25, "🎧", "Audio", 4.7),
        ("Orbit Speaker", "360° room-filling audio.", 129.50m, 40, "🔊", "Audio", 4.5),
        ("Echo Earbuds", "Active noise cancelling buds.", 179m, 60, "🎵", "Audio", 4.4),
        ("Vinyl Turntable", "Belt-drive hi-fi turntable.", 329m, 9, "💿", "Audio", 4.6),
        ("Nebula Watch", "Sapphire glass smartwatch.", 399m, 12, "⌚", "Wearables", 4.6),
        ("Pulse Band", "7-day battery fitness tracker.", 79m, 80, "📿", "Wearables", 4.2),
        ("Halo Ring", "Sleep and recovery smart ring.", 299m, 15, "💍", "Wearables", 4.3),
        ("Vision Glasses", "AR smart glasses.", 549m, 7, "🕶️", "Wearables", 4.1),
        ("Prism Camera", "Mirrorless 4K creator camera.", 1199m, 6, "📷", "Cameras", 4.8),
        ("Drone Falcon", "Foldable 4K camera drone.", 749m, 11, "🛸", "Cameras", 4.5),
        ("Action Cam Pro", "Waterproof 5K action camera.", 349m, 30, "🎥", "Cameras", 4.4),
        ("Retro Instant Cam", "Instant prints, pastel finish.", 119m, 35, "📸", "Cameras", 4.0),
        ("Nova Console", "Next-gen 4K gaming console.", 499m, 20, "🎮", "Gaming", 4.8),
        ("Apex Controller", "Pro pad with hall-effect sticks.", 89m, 50, "🕹️", "Gaming", 4.5),
        ("Lumen Smart Lamp", "Voice-controlled mood light.", 59m, 70, "💡", "Home", 4.3),
        ("Aero Air Purifier", "HEPA purifier, whisper quiet.", 219m, 18, "🌬️", "Home", 4.6),
    ];

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Product>().Property(p => p.Price).HasPrecision(18, 2);
        b.Entity<Product>().HasIndex(p => p.Name);
        b.Entity<Product>().HasIndex(p => p.Category);
        b.Entity<Order>().Property(o => o.Total).HasPrecision(18, 2);
        b.Entity<Order>().Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        b.Entity<Order>().HasIndex(o => o.CustomerEmail);
        b.Entity<OrderLine>().Property(l => l.UnitPrice).HasPrecision(18, 2);
        b.Entity<Product>().HasData(Seed.Select((s, i) => new Product
        { Id = i + 1, Name = s.N, Description = s.D, Price = s.P, Stock = s.S, ImageUrl = s.I, Category = s.C, Rating = s.R }));
    }
}
