using System.Globalization;
using AdaManagement.Client;
using AdaManagement.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var hu = new CultureInfo("hu-HU");
CultureInfo.DefaultThreadCurrentCulture = hu;
CultureInfo.DefaultThreadCurrentUICulture = hu;

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<LocalCache>();
builder.Services.AddScoped<UndoService>();
builder.Services.AddScoped<ThemeService>();
// Open-Meteo: külön HttpClient, base address és egyedi fejlécek nélkül.
builder.Services.AddScoped(sp => new WeatherService(new HttpClient(), sp.GetRequiredService<LocalCache>()));
builder.Services.AddMudServices();
builder.Services.AddTransient<MudBlazor.MudLocalizer, HungarianMudLocalizer>(); // „Required” helyett „Kötelező mező.” stb.

await builder.Build().RunAsync();
