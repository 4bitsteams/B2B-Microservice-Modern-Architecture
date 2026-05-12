using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using B2B.Web.Services.Auth;
using B2B.Web.Services.Basket;
using B2B.Web.Services.Discounts;
using B2B.Web.Services.Orders;
using B2B.Web.Services.Products;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<B2B.Web.App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var gatewayUrl = builder.Configuration["GatewayUrl"] ?? "http://localhost:5000";

// ── Core ──────────────────────────────────────────────────────────────────────
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<AuthStateProvider>());
builder.Services.AddTransient<AuthMessageHandler>();

// ── HTTP Clients ──────────────────────────────────────────────────────────────
// Public — login / register (no JWT header)
builder.Services.AddHttpClient("public",
    c => c.BaseAddress = new Uri(gatewayUrl));

// Authenticated — all domain API calls
builder.Services.AddHttpClient("auth",
    c => c.BaseAddress = new Uri(gatewayUrl))
    .AddHttpMessageHandler<AuthMessageHandler>();

// ── Domain Services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IBasketService, BasketService>();
builder.Services.AddScoped<IDiscountService, DiscountService>();

await builder.Build().RunAsync();
