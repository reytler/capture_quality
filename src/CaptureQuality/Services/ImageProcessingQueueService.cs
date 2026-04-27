using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaptureQuality.Models;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.SignalR.Client;

namespace CaptureQuality.Services;

public class ImageProcessingQueueService : IAsyncDisposable
{
    private const int MaxUploadBytes = 10 * 1024 * 1024;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, ProcessingQueueItem> _queue = new();
    private readonly ConcurrentDictionary<string, CapturedImagePayload> _uploads = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _completionSources = new();
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
    private HubConnection? _hubConnection;

    public ImageProcessingQueueService(HttpClient http)
    {
        _http = http;
    }

    public event Action<string, int>? OnProgressUpdate;
    public event Action<string>? OnItemCompleted;
    public event Action? OnQueueChanged;

    public IReadOnlyList<ProcessingQueueItem> Items => _queue.Values.OrderByDescending(x => x.Timestamp).ToList();

    public async Task<ProcessingQueueItem> AddAsync(CapturedImagePayload image)
    {
        if (image is null)
        {
            throw new ArgumentNullException(nameof(image));
        }

        var item = new ProcessingQueueItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Thumbnail = image.ThumbnailUrl,
            FileName = image.FileName,
            ContentType = image.ContentType,
            Status = QueueItemStatus.Pending,
            Stage = "queued",
            Message = "Aguardando processamento"
        };

        _queue[item.Id] = item;
        _uploads[item.Id] = image;

        OnQueueChanged?.Invoke();
        _ = ProcessQueueAsync();
        return item;
    }

    private async Task ProcessQueueAsync()
    {
        if (!await _processingSemaphore.WaitAsync(0))
        {
            return;
        }

        try
        {
            while (true)
            {
                var pending = _queue.Values.FirstOrDefault(x => x.Status == QueueItemStatus.Pending);
                if (pending is null)
                {
                    break;
                }

                if (!_uploads.TryGetValue(pending.Id, out var upload))
                {
                    pending.Status = QueueItemStatus.Failed;
                    pending.ErrorMessage = "Upload payload not found";
                    OnQueueChanged?.Invoke();
                    continue;
                }

                pending.Status = QueueItemStatus.Processing;
                pending.Progress = 5;
                pending.Stage = "uploading";
                pending.Message = "Enviando imagem";
                OnProgressUpdate?.Invoke(pending.Id, pending.Progress);
                OnQueueChanged?.Invoke();

                try
                {
                    await EnsureHubConnectionAsync();

                    using var form = new MultipartFormDataContent();
                    using var fileContent = CreateFileContent(upload);
                    form.Add(fileContent, "file", upload.FileName);

                    using var response = await _http.PostAsync("api/blur-jobs", form);
                    if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                    {
                        throw new InvalidOperationException("Image exceeds 10MB limit");
                    }

                    response.EnsureSuccessStatusCode();

                    var created = await response.Content.ReadFromJsonAsync<CreateBlurJobResponse>();
                    if (created is null || string.IsNullOrWhiteSpace(created.JobId))
                    {
                        throw new InvalidOperationException("Empty blur-job response");
                    }

                    pending.ServerJobId = created.JobId;
                    pending.Progress = created.Progress;
                    pending.Stage = "queued";
                    pending.Message = "Imagem recebida pelo servidor";

                    var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _completionSources[created.JobId] = completion;

                    await _hubConnection!.InvokeAsync("Subscribe", created.JobId);

                    var snapshot = await _http.GetFromJsonAsync<BlurJobUpdateDto>($"api/blur-jobs/{created.JobId}");
                    if (snapshot is not null)
                    {
                        ApplyServerUpdate(snapshot);
                    }

                    await completion.Task;
                    if (_hubConnection is not null)
                    {
                        await _hubConnection.InvokeAsync("Unsubscribe", created.JobId);
                    }

                    _uploads.TryRemove(pending.Id, out _);
                }
                catch (Exception ex)
                {
                    pending.Status = QueueItemStatus.Failed;
                    pending.ErrorMessage = ex.Message;
                    pending.Stage = "failed";
                    pending.Message = "Falha no processamento";
                    Console.WriteLine($"[ImageProcessingQueueService] Item {pending.Id} FAILED: {ex.Message}");
                }
                finally
                {
                    if (pending.ServerJobId is not null)
                    {
                        _completionSources.TryRemove(pending.ServerJobId, out _);
                    }

                    OnQueueChanged?.Invoke();
                }
            }
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private async Task EnsureHubConnectionAsync()
    {
        if (_hubConnection is not null)
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
            }

            return;
        }

        var hubUri = new Uri(_http.BaseAddress!, "hubs/blur-jobs");
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.HttpMessageHandlerFactory = handler => handler;
            })
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<BlurJobUpdateDto>("JobUpdated", ApplyServerUpdate);
        await _hubConnection.StartAsync();
    }

    private void ApplyServerUpdate(BlurJobUpdateDto update)
    {
        var item = _queue.Values.FirstOrDefault(x => x.ServerJobId == update.JobId);
        if (item is null)
        {
            return;
        }

        item.Progress = update.Progress;
        item.Stage = update.Stage;
        item.Message = update.Message;
        item.ErrorMessage = update.Error;
        item.Result = update.Result is null
            ? item.Result
            : new BlurDetectionResult
            {
                IsAccepted = update.Result.IsAccepted,
                BlurRatio = update.Result.BlurRatio,
                TotalPatches = update.Result.TotalPatches,
                BlurredPatches = update.Result.BlurredPatches,
                Status = update.Result.Status,
                PatchSize = update.Result.PatchSize,
                ImageWidth = update.Result.ImageWidth,
                ImageHeight = update.Result.ImageHeight,
                BlurMap = null
            };

        item.Status = MapStatus(update.Status);
        OnProgressUpdate?.Invoke(item.Id, item.Progress);

        if (item.Status == QueueItemStatus.Completed)
        {
            OnItemCompleted?.Invoke(item.Id);
            CompleteJob(update.JobId);
        }
        else if (item.Status is QueueItemStatus.Failed or QueueItemStatus.Canceled)
        {
            CompleteJob(update.JobId);
        }

        OnQueueChanged?.Invoke();
    }

    private void CompleteJob(string jobId)
    {
        if (_completionSources.TryGetValue(jobId, out var completion))
        {
            completion.TrySetResult(true);
        }
    }

    private static ByteArrayContent CreateByteArrayContent(CapturedImagePayload upload)
    {
        if (upload.Bytes is null || upload.Bytes.Length == 0)
        {
            throw new InvalidOperationException("Missing image bytes");
        }

        if (upload.Bytes.Length > MaxUploadBytes)
        {
            throw new InvalidOperationException("Image exceeds 10MB limit");
        }

        var content = new ByteArrayContent(upload.Bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(upload.ContentType);
        return content;
    }

    private static HttpContent CreateFileContent(CapturedImagePayload upload)
    {
        if (upload.BrowserFile is IBrowserFile file)
        {
            var content = new StreamContent(file.OpenReadStream(MaxUploadBytes));
            content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            return content;
        }

        return CreateByteArrayContent(upload);
    }

    private static QueueItemStatus MapStatus(string status)
    {
        return status switch
        {
            "Queued" => QueueItemStatus.Pending,
            "Running" => QueueItemStatus.Processing,
            "Completed" => QueueItemStatus.Completed,
            "Canceled" => QueueItemStatus.Canceled,
            "Failed" => QueueItemStatus.Failed,
            _ => QueueItemStatus.Processing
        };
    }

    public void Clear()
    {
        _queue.Clear();
        _uploads.Clear();
        OnQueueChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _processingSemaphore.Dispose();

        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
