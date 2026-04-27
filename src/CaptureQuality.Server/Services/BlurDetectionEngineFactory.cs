using CaptureQuality.Models;
using CaptureQuality.Server.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CaptureQuality.Server.Services;

public sealed class BlurDetectionEngineFactory : IBlurDetectionEngine
{
    private readonly LegacySvdBlurDetectionEngine _legacyEngine;
    private readonly OpenCvSpikeBlurDetectionEngine _openCvSpikeEngine;
    private readonly ILogger<BlurDetectionEngineFactory> _logger;
    private readonly IOptionsMonitor<ServerBlurDetectionOptions> _optionsMonitor;

    public string Name => GetSelectedEngine().Name;

    public BlurDetectionEngineFactory(
        LegacySvdBlurDetectionEngine legacyEngine,
        OpenCvSpikeBlurDetectionEngine openCvSpikeEngine,
        ILogger<BlurDetectionEngineFactory> logger,
        IOptionsMonitor<ServerBlurDetectionOptions> optionsMonitor)
    {
        _legacyEngine = legacyEngine;
        _openCvSpikeEngine = openCvSpikeEngine;
        _logger = logger;
        _optionsMonitor = optionsMonitor;
    }

    public Task<BlurDetectionResult> DetectBlurAsync(
        Stream imageStream,
        Func<int, Task>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var engine = GetSelectedEngine();
        _logger.LogInformation("Using blur detection engine {EngineName}", engine.Name);
        return engine.DetectBlurAsync(imageStream, onProgress, cancellationToken);
    }

    private IBlurDetectionEngine GetSelectedEngine()
    {
        return _optionsMonitor.CurrentValue.Engine switch
        {
            BlurDetectionEngineKind.OpenCvSpike => _openCvSpikeEngine,
            _ => _legacyEngine
        };
    }
}
