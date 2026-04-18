using System;
using System.IO;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtConverter.App.Models;

namespace YtConverter.App.ViewModels;

public partial class JobViewModel : ObservableObject
{
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private OutputFormat _format;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private JobStatus _status = JobStatus.Idle;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusText = "대기 중";
    [ObservableProperty] private string _outputPath = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    public CancellationTokenSource? Cts { get; set; }
    public Action<JobViewModel>? RequestRemove { get; set; }

    public string FormatText => Format == OutputFormat.Mp3 ? "MP3" : "MP4";
    public string DisplayTitle => string.IsNullOrEmpty(Title)
        ? (Url.Length > 60 ? Url[..60] + "…" : Url)
        : Title;

    partial void OnTitleChanged(string value) => OnPropertyChanged(nameof(DisplayTitle));
    partial void OnUrlChanged(string value) => OnPropertyChanged(nameof(DisplayTitle));
    partial void OnFormatChanged(OutputFormat value) => OnPropertyChanged(nameof(FormatText));

    partial void OnStatusChanged(JobStatus value)
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsRemovable));
        OnPropertyChanged(nameof(StatusGlyph));
        CancelCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
    }

    public bool IsRunning => Status is JobStatus.Resolving or JobStatus.Downloading or JobStatus.Muxing;
    public bool IsRemovable => Status is JobStatus.Idle or JobStatus.Completed or JobStatus.Failed or JobStatus.Canceled;

    public string StatusGlyph => Status switch
    {
        JobStatus.Idle => "⏳",
        JobStatus.Resolving => "🔎",
        JobStatus.Downloading => "⬇",
        JobStatus.Muxing => "⚙",
        JobStatus.Completed => "✅",
        JobStatus.Failed => "❌",
        JobStatus.Canceled => "⏹",
        _ => "·"
    };

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        Cts?.Cancel();
        StatusText = "취소 중...";
    }
    private bool CanCancel() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanRemove))]
    private void Remove() => RequestRemove?.Invoke(this);
    private bool CanRemove() => IsRemovable;

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        if (string.IsNullOrEmpty(OutputPath)) return;
        var dir = Path.GetDirectoryName(OutputPath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{OutputPath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }
    private bool CanOpenFolder() => Status == JobStatus.Completed && File.Exists(OutputPath);

    partial void OnOutputPathChanged(string value) => OpenFolderCommand.NotifyCanExecuteChanged();
}
