using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CaptureQuality;
using CaptureQuality.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton<BlurDetectorService>();
builder.Services.AddSingleton<ImageProcessorService>();
builder.Services.AddSingleton<SvdAnalyzerService>();

await builder.Build().RunAsync();