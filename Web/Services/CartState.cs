namespace Shop.Web.Services;

public class CartState
{
    private readonly Dictionary<int, (ProductDto Product, int Qty)> _items = new();
    public event Action? Changed;
    public IReadOnlyCollection<(ProductDto Product, int Qty)> Items => _items.Values;
    public int Count => _items.Values.Sum(i => i.Qty);
    public decimal Total => _items.Values.Sum(i => i.Product.Price * i.Qty);

    public void Add(ProductDto p)
    {
        var qty = _items.TryGetValue(p.Id, out var e) ? e.Qty : 0;
        if (qty < p.Stock) _items[p.Id] = (p, qty + 1);
        Changed?.Invoke();
    }
    public void Remove(int id)
    {
        if (_items.TryGetValue(id, out var e))
        {
            if (e.Qty <= 1) _items.Remove(id); else _items[id] = (e.Product, e.Qty - 1);
        }
        Changed?.Invoke();
    }
    public void Clear() { _items.Clear(); Changed?.Invoke(); }
}
