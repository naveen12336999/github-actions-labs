using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Shop.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ShopDb>(o => o.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<ShopDb>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler(e => e.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    app.Logger.LogError(ex, "Unhandled exception");
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { title = "Unexpected error", status = 500 });
}));
app.UseCors();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }

using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ShopDb>().Database.EnsureCreated();

app.MapHealthChecks("/health");
var api = app.MapGroup("/api");

static IResult? Validate<T>(T model) where T : notnull
{
    var errors = new List<ValidationResult>();
    if (Validator.TryValidateObject(model, new ValidationContext(model), errors, true)) return null;
    return Results.ValidationProblem(errors.ToDictionary(
        r => r.MemberNames.FirstOrDefault() ?? "", r => new[] { r.ErrorMessage ?? "Invalid" }));
}
static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Description, p.Price, p.Stock, p.ImageUrl);

// ---- Products ----
var products = api.MapGroup("/products");
products.MapGet("/", async (ShopDb db, string? q, CancellationToken ct) =>
{
    var query = db.Products.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => EF.Functions.Like(p.Name, $"%{q}%"));
    var productsList = await query
    .OrderBy(p => p.Name)
    .ToListAsync(ct);

return Results.Ok(productsList.Select(p => ToDto(p)));
});
products.MapGet("/{id:int}", async (int id, ShopDb db) =>
    await db.Products.FindAsync(id) is { } p ? Results.Ok(ToDto(p)) : Results.NotFound());
products.MapPost("/", async (ProductInput i, ShopDb db) =>
{
    if (Validate(i) is { } bad) return bad;
    var p = new Product { Name = i.Name, Description = i.Description, Price = i.Price, Stock = i.Stock, ImageUrl = i.ImageUrl };
    db.Products.Add(p); await db.SaveChangesAsync();
    return Results.Created($"/api/products/{p.Id}", ToDto(p));
});
products.MapPut("/{id:int}", async (int id, ProductInput i, ShopDb db) =>
{
    if (Validate(i) is { } bad) return bad;
    if (await db.Products.FindAsync(id) is not { } p) return Results.NotFound();
    (p.Name, p.Description, p.Price, p.Stock, p.ImageUrl) = (i.Name, i.Description, i.Price, i.Stock, i.ImageUrl);
    await db.SaveChangesAsync(); return Results.Ok(ToDto(p));
});
products.MapDelete("/{id:int}", async (int id, ShopDb db) =>
    await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync() > 0 ? Results.NoContent() : Results.NotFound());

// ---- Orders ----
var orders = api.MapGroup("/orders");
orders.MapPost("/", async (OrderInput i, ShopDb db) =>
{
    if (Validate(i) is { } bad) return bad;
    foreach (var l in i.Lines) if (Validate(l) is { } badLine) return badLine;

    await using var tx = await db.Database.BeginTransactionAsync();
    var ids = i.Lines.Select(l => l.ProductId).Distinct().ToList();
    var found = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
    var order = new Order { CustomerEmail = i.CustomerEmail };

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
    return Results.Created($"/api/orders/{order.Id}", new OrderDto(order.Id, order.CreatedUtc, order.CustomerEmail, order.Total,
        order.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice))));
});
orders.MapGet("/{id:int}", async (int id, ShopDb db) =>
    await db.Orders.AsNoTracking().Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id) is { } o
        ? Results.Ok(new OrderDto(o.Id, o.CreatedUtc, o.CustomerEmail, o.Total,
            o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice))))
        : Results.NotFound());

app.Run();
