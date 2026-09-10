using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;

namespace FreeDM;

public sealed class DownloadManager
{
    private readonly HttpClient _client;

    private readonly Dictionary<
        DownloadItem,
        CancellationTokenSource> _activeDownloads = new();

    private readonly object _lock = new();

    public DownloadManager()
    {
        _client = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        _client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "FreeDM/1.0");
    }

    public async Task StartAsync(
        DownloadItem item)
    {
        CancellationTokenSource cts;

        lock (_lock)
        {
            if (_activeDownloads.ContainsKey(item))
                return;

            cts = new CancellationTokenSource();
            _activeDownloads[item] = cts;
        }

        try
        {
            await DownloadAsync(item, cts.Token);
        }
        finally
        {
            lock (_lock)
            {
                _activeDownloads.Remove(item);
            }

            cts.Dispose();
        }
    }

    public void Pause(DownloadItem item)
    {
        lock (_lock)
        {
            if (_activeDownloads.TryGetValue(
                    item,
                    out var cts))
            {
                item.State = DownloadState.Paused;
                cts.Cancel();
            }
        }
    }

    public void Cancel(DownloadItem item)
    {
        lock (_lock)
        {
            if (_activeDownloads.TryGetValue(
                    item,
                    out var cts))
            {
                item.State = DownloadState.Cancelled;
                cts.Cancel();
            }
        }
    }

    private async Task DownloadAsync(
        DownloadItem item,
        CancellationToken token)
    {
        try
        {
            if (!Uri.TryCreate(
                    item.Url,
                    UriKind.Absolute,
                    out var uri))
            {
                item.State = DownloadState.Failed;
                item.Status = "Invalid URL";
                return;
            }

            string? directory =
                Path.GetDirectoryName(item.FilePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            long existingBytes = 0;

            if (File.Exists(item.FilePath))
            {
                existingBytes =
                    new FileInfo(item.FilePath).Length;
            }

            using var request =
                new HttpRequestMessage(
                    HttpMethod.Get,
                    uri);

            if (existingBytes > 0)
            {
                request.Headers.Range =
                    new System.Net.Http.Headers.RangeHeaderValue(
                        existingBytes,
                        null);
            }

            using var response =
                await _client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    token);

            bool resumed =
                existingBytes > 0 &&
                response.StatusCode ==
                HttpStatusCode.PartialContent;

            if (existingBytes > 0 && !resumed)
            {
                existingBytes = 0;
            }

            response.EnsureSuccessStatusCode();

            long contentLength =
                response.Content.Headers.ContentLength ?? -1;

            long totalBytes =
                contentLength >= 0
                    ? existingBytes + contentLength
                    : -1;

            if (totalBytes > 0)
                item.Size = FormatBytes(totalBytes);
            else
                item.Size = "Unknown";

            item.State = DownloadState.Downloading;

            FileMode mode =
                resumed
                    ? FileMode.Append
                    : FileMode.Create;

            await using Stream input =
                await response.Content.ReadAsStreamAsync(token);

            await using FileStream output =
                new FileStream(
                    item.FilePath,
                    mode,
                    FileAccess.Write,
                    FileShare.None,
                    128 * 1024,
                    FileOptions.Asynchronous |
                    FileOptions.SequentialScan);

            byte[] buffer = new byte[128 * 1024];

            long downloaded = existingBytes;
            long previousBytes = downloaded;

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (true)
            {
                int bytesRead =
                    await input.ReadAsync(
                        buffer.AsMemory(),
                        token);

                if (bytesRead == 0)
                    break;

                await output.WriteAsync(
                    buffer.AsMemory(0, bytesRead),
                    token);

                downloaded += bytesRead;

                if (totalBytes > 0)
                {
                    item.Progress =
                        downloaded * 100.0 /
                        totalBytes;
                }

                if (stopwatch.ElapsedMilliseconds >= 500)
                {
                    long difference =
                        downloaded - previousBytes;

                    double seconds =
                        stopwatch.Elapsed.TotalSeconds;

                    if (seconds > 0)
                    {
                        long bytesPerSecond =
                            (long)(difference / seconds);

                        item.Speed =
                            $"{FormatBytes(bytesPerSecond)}/s";
                    }

                    previousBytes = downloaded;
                    stopwatch.Restart();
                }
            }

            item.Progress = 100;
            item.Speed = "Complete";
            item.State = DownloadState.Completed;
        }
        catch (OperationCanceledException)
        {
            // Pause() / Cancel() already set the appropriate state.
            if (item.State == DownloadState.Downloading)
                item.State = DownloadState.Paused;
        }
        catch (Exception ex)
        {
            item.State = DownloadState.Failed;
            item.Speed = "-";
            item.Status = ex.Message;
        }
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0)
            return "Unknown";

        string[] units =
        {
            "B",
            "KB",
            "MB",
            "GB",
            "TB"
        };

        double value = bytes;
        int unit = 0;

        while (value >= 1024 &&
               unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }
}
