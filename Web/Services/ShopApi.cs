using System.Net;
namespace Shop.Web.Services;

public record ProductDto(int Id, string Name, string? Description, decimal Price, int Stock, string? ImageUrl);
public record OrderLineInput(int ProductId, int Quantity);
public record OrderInput(string CustomerEmail, List<OrderLineInput> Lines);
public record OrderResult(int Id, decimal Total);

/// <summary>Typed client for the Shop API (registered via AddHttpClient&lt;ShopApi&gt;).</summary>
public class ShopApi(HttpClient http)
{
    public async Task<List<ProductDto>> ListProductsAsync(string? query = null, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ProductDto>>($"api/products?q={Uri.EscapeDataString(query ?? "")}", ct) ?? [];

    public async Task<(OrderResult? Order, HttpStatusCode Status)> PlaceOrderAsync(OrderInput input, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("api/orders", input, ct);
        return (res.IsSuccessStatusCode ? await res.Content.ReadFromJsonAsync<OrderResult>(ct) : null, res.StatusCode);
    }
}
