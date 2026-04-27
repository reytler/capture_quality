using CaptureQuality.Server.Contracts;
using CaptureQuality.Services;
using Microsoft.AspNetCore.Http.Features;

const long MaxUploadBytes = 10L * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxUploadBytes);

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxUploadBytes;
});

builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton<BlurDetectorService>();
builder.Services.AddSingleton<ImageProcessorService>();
builder.Services.AddSingleton<SvdAnalyzerService>();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

static async Task<IResult> BlurDetectionHandler(HttpRequest request, BlurDetectorService detector)
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest("Expected multipart/form-data");
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null)
    {
        return Results.BadRequest("Missing form file 'file'");
    }

    if (file.Length <= 0)
    {
        return Results.BadRequest("Empty upload");
    }

    if (file.Length > MaxUploadBytes)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest("Unsupported content type");
    }

    await using var stream = file.OpenReadStream();

    var result = await detector.DetectBlurAsync(stream);

    var dto = new BlurDetectionMetricsDto
    {
        IsAccepted = result.IsAccepted,
        BlurRatio = result.BlurRatio,
        TotalPatches = result.TotalPatches,
        BlurredPatches = result.BlurredPatches,
        Status = result.Status,
        PatchSize = result.PatchSize,
        ImageWidth = result.ImageWidth,
        ImageHeight = result.ImageHeight
    };

    return Results.Ok(dto);
}

app.MapPost("/api/blur-detection", BlurDetectionHandler);

// Ensure API paths never hit the SPA fallback (which can surface as 405 for POST).
app.MapMethods("/api/{**path}", ["GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD"], () => Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();
