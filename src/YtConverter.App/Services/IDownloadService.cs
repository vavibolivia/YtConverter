using System;
using System.Threading;
using System.Threading.Tasks;
using YtConverter.App.Models;

namespace YtConverter.App.Services;

public interface IDownloadService
{
    Task<ConversionResult> ConvertAsync(
        string url,
        OutputFormat format,
        string outputFolder,
        IProgress<ConversionProgress> progress,
        CancellationToken ct);
}

public sealed record ConversionProgress(JobStatus Status, double Ratio, string? VideoTitle);

public sealed record ConversionResult(string OutputPath, string VideoTitle, TimeSpan Duration);
