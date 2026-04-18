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
    public Action<JobViewModel>? RequestRetry { get; set; }
    public Action? StateChanged { get; set; }

    public Models.JobSnapshot ToSnapshot() => new()
    {
        Url = Url,
        Format = Format,
        Status = Status,
        Title = Title,
        OutputPath = OutputPath,
        ErrorMessage = ErrorMessage
    };

    public static JobViewModel FromSnapshot(Models.JobSnapshot s)
    {
        // 실행 중이었던 작업은 Idle 로 복원해 재시도 대상이 되게 함
        var restoredStatus = s.Status switch
        {
            JobStatus.Resolving or JobStatus.Downloading or JobStatus.Muxing => JobStatus.Idle,
            _ => s.Status
        };
        return new JobViewModel
        {
            Url = s.Url,
            Format = s.Format,
            Title = s.Title,
            OutputPath = s.OutputPath,
            ErrorMessage = s.ErrorMessage,
            Status = restoredStatus,
            StatusText = restoredStatus == JobStatus.Idle ? "대기 중 (재개됨)" :
                         restoredStatus == JobStatus.Completed ? "완료" :
                         restoredStatus == JobStatus.Failed ? "오류" :
                         restoredStatus == JobStatus.Canceled ? "취소됨" : "대기 중",
            Progress = restoredStatus == JobStatus.Completed ? 1.0 : 0
        };
    }

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
        OnPropertyChanged(nameof(IsMuxing));
        OnPropertyChanged(nameof(IsActive));
        CancelCommand.NotifyCanExecuteChanged();
        RemoveCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        StateChanged?.Invoke();
    }

    public bool IsMuxing => Status == JobStatus.Muxing;
    public bool IsActive => Status is JobStatus.Resolving or JobStatus.Downloading or JobStatus.Muxing;

    partial void OnErrorMessageChanged(string? value) => StateChanged?.Invoke();

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

    [RelayCommand(CanExecute = nameof(CanRetry))]
    private void Retry() => RequestRetry?.Invoke(this);
    private bool CanRetry() => Status is JobStatus.Failed or JobStatus.Canceled;

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

    partial void OnOutputPathChanged(string value)
    {
        OpenFolderCommand.NotifyCanExecuteChanged();
        StateChanged?.Invoke();
    }
}
