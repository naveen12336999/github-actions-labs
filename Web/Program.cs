using Shop.Web.Components;
using Shop.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Typed HttpClient: API calls now run server-side, so the browser never talks to the API (no CORS needed).
builder.Services.AddHttpClient<ShopApi>(c =>
    c.BaseAddress = new Uri(builder.Configuration["ApiUrl"] ?? "http://localhost:5080/"));

// Scoped = one cart per Blazor circuit (per browser tab session).
builder.Services.AddScoped<CartState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
