using CaptureQuality.Models;
using CaptureQuality.Server.Configuration;
using CaptureQuality.Server.Hubs;
using CaptureQuality.Server.Services;
using CaptureQuality.Services;
using Microsoft.AspNetCore.Http.Features;

const long MaxUploadBytes = 10L * 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = MaxUploadBytes);

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = MaxUploadBytes;
});

builder.Services.AddSignalR();
builder.Services.Configure<ServerBlurDetectionOptions>(builder.Configuration.GetSection(ServerBlurDetectionOptions.SectionName));
builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton<BlurDetectorService>();
builder.Services.AddSingleton<ImageProcessorService>();
builder.Services.AddSingleton<SvdAnalyzerService>();
builder.Services.AddSingleton<LegacySvdBlurDetectionEngine>();
builder.Services.AddSingleton<OpenCvSpikeBlurDetectionEngine>();
builder.Services.AddSingleton<IBlurDetectionEngine, BlurDetectionEngineFactory>();
builder.Services.AddSingleton<IBlurJobStore, InMemoryBlurJobStore>();
builder.Services.AddHostedService<BlurJobProcessor>();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

static async Task<(string FileName, string ContentType, byte[] Bytes)?> ReadUploadAsync(HttpRequest request)
{
    if (!request.HasFormContentType)
    {
        return null;
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file");
    if (file is null)
    {
        return null;
    }

    if (file.Length <= 0)
    {
        return null;
    }

    if (file.Length > MaxUploadBytes)
    {
        throw new BadHttpRequestException("Payload too large", StatusCodes.Status413PayloadTooLarge);
    }

    if (string.IsNullOrWhiteSpace(file.ContentType) || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
    {
        throw new BadHttpRequestException("Unsupported content type", StatusCodes.Status400BadRequest);
    }

    await using var stream = file.OpenReadStream();
    using var buffer = new MemoryStream();
    await stream.CopyToAsync(buffer);

    return (file.FileName, file.ContentType, buffer.ToArray());
}

static async Task<IResult> BlurDetectionHandler(HttpRequest request, IBlurDetectionEngine detector, CancellationToken cancellationToken)
{
    try
    {
        var logger = request.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BlurDetectionHandler");
        var upload = await ReadUploadAsync(request);
        if (upload is null)
        {
            return Results.BadRequest("Expected multipart/form-data with form file 'file'");
        }

        logger.LogInformation("Handling synchronous blur detection with engine {EngineName}", detector.Name);

        using var stream = new MemoryStream(upload.Value.Bytes, writable: false);
        var result = await detector.DetectBlurAsync(stream, cancellationToken: cancellationToken);

        logger.LogInformation("Completed synchronous blur detection with engine {EngineName} and status {Status}", detector.Name, result.Status);
        return Results.Ok(result.ToDto());
    }
    catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }
    catch (BadHttpRequestException ex)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (Exception ex)
    {
        var logger = request.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BlurDetectionHandler");
        logger.LogError(ex, "Synchronous blur detection failed");
        return Results.Problem(title: "Blur detection failed", detail: ex.Message);
    }
}

static async Task<IResult> CreateBlurJobHandler(HttpRequest request, IBlurJobStore jobStore, CancellationToken cancellationToken)
{
    try
    {
        var upload = await ReadUploadAsync(request);
        if (upload is null)
        {
            return Results.BadRequest("Expected multipart/form-data with form file 'file'");
        }

        var response = await jobStore.EnqueueAsync(upload.Value.FileName, upload.Value.ContentType, upload.Value.Bytes, cancellationToken);
        return Results.Accepted($"/api/blur-jobs/{response.JobId}", response);
    }
    catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }
    catch (BadHttpRequestException ex)
    {
        return Results.BadRequest(ex.Message);
    }
}

app.MapPost("/api/blur-detection", BlurDetectionHandler);
app.MapPost("/api/blur-jobs", CreateBlurJobHandler);
app.MapGet("/api/blur-jobs/{jobId}", (string jobId, IBlurJobStore jobStore) =>
{
    var job = jobStore.Get(jobId);
    return job is null ? Results.NotFound() : Results.Ok(job);
});
app.MapDelete("/api/blur-jobs/{jobId}", (string jobId, IBlurJobStore jobStore) =>
{
    return jobStore.TryCancel(jobId) ? Results.Accepted() : Results.NotFound();
});
app.MapHub<BlurJobHub>("/hubs/blur-jobs");

// Ensure API paths never hit the SPA fallback (which can surface as 405 for POST).
app.MapMethods("/api/{**path}", ["GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD"], () => Results.NotFound());

app.MapFallbackToFile("index.html");

app.Run();
