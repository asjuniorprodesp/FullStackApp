using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ClientApp;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["Api:BaseUrl"];

if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
	throw new InvalidOperationException("Missing configuration key 'Api:BaseUrl'.");
}

builder.Services.AddScoped(sp => new HttpClient
{
	BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute)
});
builder.Services.AddScoped<ClientApp.Services.ProductCatalogService>();

await builder.Build().RunAsync();
