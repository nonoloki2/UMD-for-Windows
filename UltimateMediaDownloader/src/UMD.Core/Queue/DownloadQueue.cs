using System.Collections.Concurrent;
using UMD.Core.Engine;
using UMD.Core.Models;
using UMD.Core.Platforms;

namespace UMD.Core.Queue;

/// <summary>
/// Um item da fila. Carrega o pedido, o estado atual e o progresso.
/// </summary>
public sealed class DownloadJob
{
    public Guid Id { get; } = Guid.NewGuid();

    public required DownloadRequest Request { get; init; }

    public Platform Platform { get; init; }

    public string Title { get; set; } = string.Empty;

    public JobStatus Status { get; set; } = JobStatus.Pending;

    public DownloadProgress? Progress { get; set; }

    public DownloadResult? Result { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.Now;

    public DateTimeOffset? CompletedAt { get; set; }

    internal CancellationTokenSource? Cancellation { get; set; }

    public void Cancel()
    {
        try
        {
            Cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // O job ja terminou. Nada a fazer.
        }
    }
}

/// <summary>
/// Fila de downloads com limite de execucoes simultaneas.
///
/// O UMD original fazia isto com ThreadPoolExecutor dentro de
/// download_batch_optimized(). Aqui um SemaphoreSlim controla a concorrencia e
/// cada job roda numa Task, sem bloquear a interface.
/// </summary>
public sealed class DownloadQueue : IDisposable
{
    private readonly YtDlpEngine _engine;
    private readonly ConcurrentDictionary<Guid, DownloadJob> _jobs = new();
    private SemaphoreSlim _slots;
    private int _maxConcurrent;

    public DownloadQueue(YtDlpEngine engine, int maxConcurrent = 3)
    {
        _engine = engine;
        _maxConcurrent = Math.Max(1, maxConcurrent);
        _slots = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
    }

    /// <summary>Disparado sempre que um job muda de estado ou de progresso.</summary>
    public event Action<DownloadJob>? JobUpdated;

    public IReadOnlyCollection<DownloadJob> Jobs => _jobs.Values.ToList();

    public int MaxConcurrent => _maxConcurrent;

    /// <summary>
    /// Ajusta o limite de downloads simultaneos. Jobs ja em execucao continuam.
    /// </summary>
    public void SetMaxConcurrent(int value)
    {
        var target = Math.Max(1, value);
        if (target == _maxConcurrent) return;

        var old = _slots;
        _slots = new SemaphoreSlim(target, target);
        _maxConcurrent = target;
        old.Dispose();
    }

    /// <summary>
    /// Coloca uma URL na fila e comeca a processar assim que houver vaga.
    /// </summary>
    public DownloadJob Enqueue(DownloadRequest request, string? title = null)
    {
        var job = new DownloadJob
        {
            Request = request,
            Platform = PlatformDetector.Detect(request.Url),
            Title = title ?? request.Url
        };

        _jobs[job.Id] = job;
        JobUpdated?.Invoke(job);

        _ = Task.Run(() => ProcessAsync(job));
        return job;
    }

    private async Task ProcessAsync(DownloadJob job)
    {
        using var cts = new CancellationTokenSource();
        job.Cancellation = cts;

        await _slots.WaitAsync(cts.Token).ConfigureAwait(false);

        try
        {
            // Busca o titulo antes de baixar, para a fila nao ficar mostrando
            // URLs cruas enquanto o download roda.
            if (job.Title == job.Request.Url)
            {
                job.Status = JobStatus.FetchingInfo;
                JobUpdated?.Invoke(job);

                try
                {
                    // Se a URL carrega uma radio/mix do YouTube e o usuario nao
                    // pediu a playlist, ignoramos o "list": consultar uma
                    // playlist infinita trava a fila.
                    var skipPlaylist = job.Request.DownloadPlaylist == false
                        || (job.Request.DownloadPlaylist is null
                            && UrlHelper.HasPlaylistParameter(job.Request.Url)
                            && !UrlHelper.IsRealPlaylist(job.Request.Url));

                    var info = await _engine
                        .GetInfoAsync(
                            job.Request.Url,
                            flatPlaylist: true,
                            noPlaylist: skipPlaylist,
                            ct: cts.Token)
                        .ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(info?.Title))
                        job.Title = info!.Title;
                }
                catch (YtDlpException)
                {
                    // Se os metadados falharem, ainda vale tentar baixar: alguns
                    // extractors so funcionam no caminho de download.
                }
            }

            job.Status = JobStatus.Downloading;
            JobUpdated?.Invoke(job);

            var progress = new Progress<DownloadProgress>(p =>
            {
                job.Progress = p;
                if (string.Equals(p.Status, "finished", StringComparison.OrdinalIgnoreCase))
                    job.Status = JobStatus.PostProcessing;
                JobUpdated?.Invoke(job);
            });

            var result = await _engine
                .DownloadAsync(job.Request, progress, cts.Token)
                .ConfigureAwait(false);

            job.Result = result;
            job.Status = result.Success ? JobStatus.Completed : JobStatus.Failed;
            job.ErrorMessage = result.ErrorMessage;
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
        }
        finally
        {
            job.CompletedAt = DateTimeOffset.Now;
            job.Cancellation = null;
            _slots.Release();
            JobUpdated?.Invoke(job);
        }
    }

    public void CancelAll()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.Status is JobStatus.Pending or JobStatus.FetchingInfo
                or JobStatus.Downloading or JobStatus.PostProcessing)
            {
                job.Cancel();
            }
        }
    }

    /// <summary>Remove da lista os jobs que ja terminaram.</summary>
    public void ClearFinished()
    {
        foreach (var job in _jobs.Values)
        {
            if (job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
                _jobs.TryRemove(job.Id, out _);
        }
    }

    public void Dispose()
    {
        CancelAll();
        _slots.Dispose();
    }
}