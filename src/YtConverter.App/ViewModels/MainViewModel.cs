using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtConverter.App.Logging;
using YtConverter.App.Models;
using YtConverter.App.Services;

namespace YtConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDownloadService _downloadService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private OutputFormat _selectedFormat = OutputFormat.Mp3;

    [ObservableProperty]
    private string _outputFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "YtConverter");

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = "대기 중";

    [ObservableProperty]
    private string _videoTitle = string.Empty;

    public ObservableCollection<string> LogLines { get; } = new();

    public bool IsMp3Selected
    {
        get => SelectedFormat == OutputFormat.Mp3;
        set { if (value) SelectedFormat = OutputFormat.Mp3; }
    }

    public bool IsMp4Selected
    {
        get => SelectedFormat == OutputFormat.Mp4;
        set { if (value) SelectedFormat = OutputFormat.Mp4; }
    }

    partial void OnSelectedFormatChanged(OutputFormat value)
    {
        OnPropertyChanged(nameof(IsMp3Selected));
        OnPropertyChanged(nameof(IsMp4Selected));
    }

    public MainViewModel(IDownloadService downloadService)
    {
        _downloadService = downloadService;
        AppLogger.Instance.AttachSink(AppendLog);
        AppLogger.Instance.Info("앱 시작");
        Directory.CreateDirectory(OutputFolder);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(Url)) return;
        _cts = new CancellationTokenSource();
        IsBusy = true;
        Progress = 0;
        StatusText = "준비 중...";
        VideoTitle = string.Empty;
        StartCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();

        var progress = new Progress<ConversionProgress>(p =>
        {
            Progress = p.Ratio;
            StatusText = p.Status switch
            {
                JobStatus.Resolving => "스트림 해석 중...",
                JobStatus.Downloading => $"다운로드 중... {p.Ratio:P0}",
                JobStatus.Muxing => "변환 중...",
                JobStatus.Completed => "완료",
                _ => p.Status.ToString()
            };
            if (!string.IsNullOrEmpty(p.VideoTitle)) VideoTitle = p.VideoTitle!;
        });

        try
        {
            var result = await _downloadService.ConvertAsync(
                Url.Trim(), SelectedFormat, OutputFolder, progress, _cts.Token);
            StatusText = $"완료: {Path.GetFileName(result.OutputPath)}";
            AppLogger.Instance.Info($"사용자 완료 확인: {result.OutputPath}");
        }
        catch (OperationCanceledException)
        {
            StatusText = "취소됨";
            AppLogger.Instance.Warn("사용자 취소");
        }
        catch (Exception ex)
        {
            StatusText = $"오류: {ex.Message}";
            AppLogger.Instance.Error("변환 실패", ex);
            MessageBox.Show(ex.Message, "변환 오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
            StartCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanStart() => !IsBusy && !string.IsNullOrWhiteSpace(Url);

    partial void OnUrlChanged(string value) => StartCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
        StatusText = "취소 중...";
    }

    private bool CanCancel() => IsBusy;

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "저장 폴더 선택",
            InitialDirectory = Directory.Exists(OutputFolder) ? OutputFolder : null
        };
        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
            AppLogger.Instance.Info($"저장 폴더 변경: {OutputFolder}");
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(OutputFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{OutputFolder}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Error("폴더 열기 실패", ex);
        }
    }

    [RelayCommand]
    private void Paste()
    {
        try
        {
            if (Clipboard.ContainsText())
                Url = Clipboard.GetText().Trim();
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Warn($"클립보드 읽기 실패: {ex.Message}");
        }
    }

    private void AppendLog(string line)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            LogLines.Add(line);
            while (LogLines.Count > 500) LogLines.RemoveAt(0);
        });
    }
}
