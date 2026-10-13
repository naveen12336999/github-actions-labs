using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Shop.Api;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Azure SQL in production (Database__Provider=SqlServer), SQLite locally.
builder.Services.AddDbContext<ShopDb>(o =>
{
    var cs = cfg.GetConnectionString("Default");
    if (cfg["Database:Provider"] == "SqlServer") o.UseSqlServer(cs, s => s.EnableRetryOnFailure());
    else o.UseSqlite(cs);
});
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApplicationInsightsTelemetry(); // uses APPLICATIONINSIGHTS_CONNECTION_STRING app setting
builder.Services.AddResponseCompression();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear(); o.KnownProxies.Clear(); // App Service front end terminates TLS
});
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("orders", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks().AddDbContextCheck<ShopDb>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(cfg.GetSection("Cors:Origins").Get<string[]>() ?? []).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseForwardedHeaders();
app.UseResponseCompression();
app.UseExceptionHandler(e => e.Run(async ctx =>
{
    app.Logger.LogError(ctx.Features.Get<IExceptionHandlerFeature>()?.Error, "Unhandled exception");
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { title = "Unexpected error", status = 500 });
}));
app.UseCors();
app.UseRateLimiter();
app.UseSwagger(); app.UseSwaggerUI();

using (var s = app.Services.CreateScope()) s.ServiceProvider.GetRequiredService<ShopDb>().Database.EnsureCreated();

static IResult? Validate<T>(T m) where T : notnull
{
    var errs = new List<ValidationResult>();
    if (Validator.TryValidateObject(m, new ValidationContext(m), errs, true)) return null;
    return Results.ValidationProblem(errs.ToDictionary(r => r.MemberNames.FirstOrDefault() ?? "", r => new[] { r.ErrorMessage ?? "Invalid" }));
}
static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Description, p.Price, p.Stock, p.ImageUrl, p.Category, p.Rating);
static OrderDto ToDto(Order o) => new(o.Id, o.CreatedUtc, o.CustomerEmail, o.Total, o.Status,
    o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice)));

// Admin endpoints require header X-Api-Key matching the Admin__ApiKey app setting (disabled if unset).
static async ValueTask<object?> RequireAdmin(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
{
    var key = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Admin:ApiKey"];
    if (string.IsNullOrEmpty(key)) return Results.Problem("Admin API disabled.", statusCode: 403);
    var given = ctx.HttpContext.Request.Headers["X-Api-Key"].ToString();
    var ok = CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    return ok ? await next(ctx) : Results.Unauthorized();
}

app.MapHealthChecks("/health");
var api = app.MapGroup("/api");

// ---- Products ----
var products = api.MapGroup("/products");
products.MapGet("/", async (ShopDb db, string? q, string? category, string? sort, int page = 1, int pageSize = 8, CancellationToken ct = default) =>
{
    page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 50);
    var query = db.Products.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => EF.Functions.Like(p.Name, $"%{q}%"));
    if (!string.IsNullOrWhiteSpace(category)) query = query.Where(p => p.Category == category);
    query = sort switch
    {
        "price_asc" => query.OrderBy(p => (double)p.Price),
        "price_desc" => query.OrderByDescending(p => (double)p.Price),
        "rating" => query.OrderByDescending(p => p.Rating).ThenBy(p => p.Name),
        _ => query.OrderBy(p => p.Name),
    };
    var total = await query.CountAsync(ct);
    var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(p => ToDto(p)).ToListAsync(ct);
    return Results.Ok(new PagedResult<ProductDto>(items, total, page, pageSize));
});
products.MapGet("/categories", async (ShopDb db, CancellationToken ct) =>
    Results.Ok(await db.Products.AsNoTracking().Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync(ct)));
products.MapGet("/{id:int}", async (int id, ShopDb db) =>
    await db.Products.FindAsync(id) is { } p ? Results.Ok(ToDto(p)) : Results.NotFound());
products.MapPost("/", async (ProductInput i, ShopDb db) =>
{
    if (Validate(i) is { } bad) return bad;
    var p = new Product { Name = i.Name, Description = i.Description, Price = i.Price, Stock = i.Stock, ImageUrl = i.ImageUrl, Category = i.Category };
    db.Products.Add(p); await db.SaveChangesAsync();
    return Results.Created($"/api/products/{p.Id}", ToDto(p));
}).AddEndpointFilter(RequireAdmin);
products.MapPut("/{id:int}", async (int id, ProductInput i, ShopDb db) =>
{
    if (Validate(i) is { } bad) return bad;
    if (await db.Products.FindAsync(id) is not { } p) return Results.NotFound();
    (p.Name, p.Description, p.Price, p.Stock, p.ImageUrl, p.Category) = (i.Name, i.Description, i.Price, i.Stock, i.ImageUrl, i.Category);
    await db.SaveChangesAsync(); return Results.Ok(ToDto(p));
}).AddEndpointFilter(RequireAdmin);
products.MapDelete("/{id:int}", async (int id, ShopDb db) =>
    await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync() > 0 ? Results.NoContent() : Results.NotFound())
    .AddEndpointFilter(RequireAdmin);

// ---- Orders ----
var orders = api.MapGroup("/orders");
orders.MapPost("/", async (OrderInput i, ShopDb db) =>
{
    if (Validate(i) is { } bad) return bad;
    foreach (var l in i.Lines) if (Validate(l) is { } badLine) return badLine;
    await using var tx = await db.Database.BeginTransactionAsync();
    var ids = i.Lines.Select(l => l.ProductId).Distinct().ToList();
    var found = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
    var order = new Order { CustomerEmail = i.CustomerEmail.Trim().ToLowerInvariant() };
    foreach (var g in i.Lines.GroupBy(l => l.ProductId))
    {
        if (!found.TryGetValue(g.Key, out var p)) return Results.NotFound(new { title = $"Product {g.Key} not found" });
        var qty = g.Sum(x => x.Quantity);
        if (p.Stock < qty) return Results.Conflict(new { title = $"Only {p.Stock} left of {p.Name}" });
        p.Stock -= qty;
        order.Lines.Add(new OrderLine { ProductId = p.Id, ProductName = p.Name, Quantity = qty, UnitPrice = p.Price });
    }
    order.Total = order.Lines.Sum(l => l.UnitPrice * l.Quantity);
    db.Orders.Add(order);
    await db.SaveChangesAsync(); await tx.CommitAsync();
    return Results.Created($"/api/orders/{order.Id}", ToDto(order));
}).RequireRateLimiting("orders");
orders.MapGet("/", async (string email, ShopDb db) =>
{
    var e = email.Trim().ToLowerInvariant();
    var list = await db.Orders.AsNoTracking().Include(o => o.Lines).Where(o => o.CustomerEmail == e)
        .OrderByDescending(o => o.Id).Take(50).ToListAsync();
    return Results.Ok(list.Select(ToDto));
});
orders.MapGet("/{id:int}", async (int id, ShopDb db) =>
    await db.Orders.AsNoTracking().Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id) is { } o
        ? Results.Ok(ToDto(o)) : Results.NotFound());
orders.MapPatch("/{id:int}/status", async (int id, StatusInput s, ShopDb db) =>
{
    if (!Enum.IsDefined(s.Status)) return Results.BadRequest(new { title = "Invalid status" });
    var o = await db.Orders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
    if (o is null) return Results.NotFound();
    if (s.Status == OrderStatus.Cancelled && o.Status != OrderStatus.Cancelled) // restock on cancel
        foreach (var l in o.Lines) await db.Products.Where(p => p.Id == l.ProductId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Stock, p => p.Stock + l.Quantity));
    o.Status = s.Status; await db.SaveChangesAsync();
    return Results.Ok(ToDto(o));
}).AddEndpointFilter(RequireAdmin);

app.Run();
