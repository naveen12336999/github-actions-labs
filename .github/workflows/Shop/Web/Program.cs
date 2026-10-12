using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Shop.Web;
using Shop.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
var api = builder.Configuration["ApiUrl"] ?? "http://localhost:5080/";
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(api) });
builder.Services.AddScoped<CartState>();
await builder.Build().RunAsync();
