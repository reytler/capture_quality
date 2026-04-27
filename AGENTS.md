# AGENTS.md - Capture Quality Project

## Project Overview

This is a document image blur detection system built with Blazor WebAssembly and ASP.NET Core. The system analyzes images using SVD (Singular Value Decomposition) to detect blur in document captures before OCR processing.

### Project Structure

```
src/
├── CaptureQuality/           # Blazor WebAssembly client
├── CaptureQuality.Server/   # ASP.NET Core server with SignalR
├── CaptureQuality.slnx      # Solution file
```

## Build Commands

### Build the entire solution
```bash
dotnet build
```

### Build specific project
```bash
dotnet build src/CaptureQuality/CaptureQuality.csproj
dotnet build src/CaptureQuality.Server/CaptureQuality.Server.csproj
```

### Run the server (serves both API and Blazor client)
```bash
cd src/CaptureQuality.Server && dotnet run
```

### Run specific project
```bash
dotnet run --project src/CaptureQuality.Server/CaptureQuality.Server.csproj
```

### Clean build artifacts
```bash
dotnet clean
```

### Publish the server
```bash
dotnet publish src/CaptureQuality.Server/CaptureQuality.Server.csproj -c Release -o ./publish
```

### Add a package to a project
```bash
dotnet add src/CaptureQuality/CaptureQuality.csproj package <PackageName>
```

## Testing

This project currently has no formal test framework configured. Manual testing is done by:

1. Running the server (`dotnet run` in CaptureQuality.Server)
2. Opening the browser at the served URL
3. Using the camera capture demo to test blur detection

## Code Style Guidelines

### .NET Configuration

The project uses `Directory.Build.props` which configures:
- **Target Framework**: net8.0
- **Language Version**: latest
- **Nullable**: enabled
- **Implicit Usings**: enabled

Always respect these settings in new code.

### Namespace Organization

```
CaptureQuality/              # Client-side Blazor components and services
CaptureQuality.Models/      # Shared DTOs and models
CaptureQuality.Services/    # Image processing and blur detection services
CaptureQuality.Server/      # Server-side code
CaptureQuality.Server.Hubs/ # SignalR hubs
CaptureQuality.Server.Services/ # Server-side services
CaptureQuality.Server.Contracts/ # Server DTOs
```

### File Naming Conventions

- **Classes**: `PascalCase.cs` (e.g., `BlurDetectorService.cs`)
- **Interfaces**: `I` prefix + PascalCase (e.g., `IBlurJobStore.cs`)
- **Enums**: PascalCase (e.g., `BlurJobState.cs`)
- **DTOs/Records**: PascalCase with suffix indicating type (e.g., `BlurDetectionMetricsDto.cs`)

### Class Structure

```csharp
// Namespace matches folder structure
namespace CaptureQuality.Services;

// Public classes use PascalCase
public class ServiceName
{
    // Private fields use _camelCase underscore prefix
    private readonly IService _dependency;
    private readonly int _someValue;
    
    // Constructor for dependency injection
    public ServiceName(IService dependency, int someValue)
    {
        _dependency = dependency;
        _someValue = someValue;
    }
    
    // Public methods use PascalCase
    public async Task<Result> DoWorkAsync(CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

### Type Usage

- **Prefer `record`** for immutable DTOs/data transfer objects
- **Use `init` setters** for properties that should only be set at initialization
- **Use `sealed`** for classes not meant to be inherited
- **Prefer `List<T>` over arrays** for collections unless performance-critical
- **Use `float[,]` for 2D image arrays** (row-major: [x, y])
- **Use `bool[,]` for masks and binary maps**
- **Use tuples** `(int x, int y, double value)` for lightweight return types
- **Use nullable reference types** (`T?`) for optional values

### Error Handling

```csharp
// Use specific exceptions for expected errors
throw new BadHttpRequestException("Message", StatusCodes.Status4xx);
throw new ArgumentNullException(nameof(param));

// Catch and handle gracefully
catch (OperationCanceledException)
{
    // Handle cancellation - this is normal
}
catch (Exception ex)
{
    // Log and handle unexpected errors
    Console.WriteLine($"Error: {ex.Message}");
}

// For result patterns, return null or a Result type rather than throwing
```

### Async/Await Patterns

```csharp
// Always accept CancellationToken in async methods
public async Task<Result> ProcessAsync(CancellationToken cancellationToken = default)
{
    // Check for cancellation periodically
    cancellationToken.ThrowIfCancellationRequested();
    
    // Use ValueTask for hot paths when possible
    public async ValueTask DisposeAsync()
}

// Use Task.Run() for CPU-bound work to avoid blocking
var result = await Task.Run(() => CpuIntensiveOperation(), cancellationToken);
```

### Logging

Use `Console.WriteLine` for debug output during development:
```csharp
Console.WriteLine($"[ServiceName:MethodName] Step description");
```

### Image Processing Patterns

- **Coordinate order**: `[x, y]` for 2D arrays (x is width/column, y is height/row)
- **Use `Image.LoadAsync<T>` with `using` for proper disposal**
- **Process in background threads** for heavy operations using `Task.Run()`
- **Report progress** via callbacks: `Action<int>? onProgress`

### SignalR Hub Pattern

```csharp
public sealed class HubName : Hub
{
    public Task Subscribe(string groupId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(groupId));
    }
    
    public static string GetGroupName(string id) => $"prefix:{id}";
}
```

### Import Organization

Order imports as:
1. System namespaces (implicit with ImplicitUsings)
2. Third-party packages
3. Project references

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using CaptureQuality.Models;
using CaptureQuality.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
```

### Key Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| SixLabors.ImageSharp | 3.1.4 | Image loading and processing |
| MathNet.Numerics | 5.0.0 | SVD and linear algebra |
| Microsoft.AspNetCore.SignalR.Client | 8.0.0 | Real-time updates from client |
| Microsoft.AspNetCore.Components.WebAssembly | 8.0.0 | Blazor client |

## Important Implementation Notes

### Blur Detection Algorithm

The blur detection uses SVD-based analysis:
- **k = 1** (number of singular values to analyze)
- **Patch size = 27** pixels
- **Patch threshold = 0.64** (Bk >= threshold means blurred)
- **Global threshold = 0.35** (blur_ratio >= 0.35 means reject)

### Configuration Service

All blur detection parameters are in `ConfigurationService.cs`:
- K, PatchSize, PatchThreshold, BlurRatioThreshold
- MedianFilterSize, GradientKernelSize, MaxImageDimension

### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | /api/blur-detection | Direct blur detection (sync) |
| POST | /api/blur-jobs | Create async job |
| GET | /api/blur-jobs/{id} | Get job status |
| DELETE | /api/blur-jobs/{id} | Cancel job |
| Hub | /hubs/blur-jobs | SignalR for real-time updates |

### Memory Management

- Always dispose `Image` objects with `using`
- Use `MemoryStream` with `using` for stream operations
- For large uploads, limit form body size (currently 10MB)