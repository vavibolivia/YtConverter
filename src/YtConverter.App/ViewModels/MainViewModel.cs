using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    private readonly LicenseService _license;
    private SemaphoreSlim _slots;

    public string TierBadge => _license.TierBadge;

    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private OutputFormat _selectedFormat = OutputFormat.Mp3;
    [ObservableProperty] private string _outputFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "YtConverter");
    [ObservableProperty] private int _maxConcurrency = 3;
    [ObservableProperty] private bool _isAnyRunning;

    public ObservableCollection<JobViewModel> Jobs { get; } = new();
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

    partial void OnMaxConcurrencyChanged(int value)
    {
        var allowed = Math.Min(Math.Max(1, value), _license?.MaxConcurrency ?? 1);
        if (allowed != value)
        {
            // 티어 한도 초과 → 복구 + 업그레이드 안내
            MaxConcurrency = allowed;
            MessageBox.Show(
                $"현재 플랜({_license?.TierBadge})에서는 동시 {_license?.MaxConcurrency}개까지 가능합니다.\n더 많은 동시 작업은 Pro/Admin 에서 사용할 수 있습니다.",
                "동시 작업 제한", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _slots = new SemaphoreSlim(allowed, allowed);
        AppLogger.Instance.Info($"동시 작업 수 변경: {allowed}");
    }

    public MainViewModel(IDownloadService downloadService, LicenseService license)
    {
        _downloadService = downloadService;
        _license = license;
        MaxConcurrency = Math.Min(MaxConcurrency, _license.MaxConcurrency);
        _slots = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        _license.LicenseChanged += () =>
        {
            OnPropertyChanged(nameof(TierBadge));
            // 티어 하향 시 동시 작업 수 제한
            if (MaxConcurrency > _license.MaxConcurrency) MaxConcurrency = _license.MaxConcurrency;
        };
        AppLogger.Instance.AttachSink(AppendLog);
        AppLogger.Instance.Info($"앱 시작 — 티어: {_license.TierBadge}");
        Directory.CreateDirectory(OutputFolder);
        Jobs.CollectionChanged += (_, __) =>
        {
            StartAllCommand.NotifyCanExecuteChanged();
            CancelAllCommand.NotifyCanExecuteChanged();
            ClearCompletedCommand.NotifyCanExecuteChanged();
        };
    }

    [RelayCommand]
    private void OpenLicense()
    {
        var dlg = new Views.LicenseDialog(_license)
        {
            Owner = Application.Current?.MainWindow
        };
        dlg.ShowDialog();
        OnPropertyChanged(nameof(TierBadge));
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(Url)) return;
        var urls = Url.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var u in urls)
        {
            var job = new JobViewModel
            {
                Url = u,
                Format = SelectedFormat,
            };
            job.RequestRemove = j => Jobs.Remove(j);
            Jobs.Add(job);
            AppLogger.Instance.Info($"작업 추가: [{job.FormatText}] {u}");
        }
        Url = string.Empty;
        StartAllCommand.NotifyCanExecuteChanged();
    }
    private bool CanAdd() => !string.IsNullOrWhiteSpace(Url);
    partial void OnUrlChanged(string value) => AddCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanStartAll))]
    private async Task StartAllAsync()
    {
        var pending = Jobs.Where(j => j.Status is JobStatus.Idle or JobStatus.Failed or JobStatus.Canceled).ToList();
        if (pending.Count == 0) return;

        // Free 티어: 한도 초과분은 미리 안내
        if (_license.Tier == UserTier.Free && pending.Count > _license.RemainingToday)
        {
            var msg = pending.Count == 1
                ? $"무료 플랜은 하루 {_license.FreeDailyQuota}건까지 가능합니다.\n지금 변환하면 한도를 초과합니다. Pro 업그레이드를 권장합니다."
                : $"무료 플랜 일일 한도: {_license.RemainingToday}건 남음 / 대기 작업: {pending.Count}건\n초과하는 작업은 자동 실패 처리됩니다. 계속할까요?";
            var r = MessageBox.Show(msg, "무료 플랜 한도", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (r != MessageBoxResult.OK) return;
        }

        IsAnyRunning = true;
        StartAllCommand.NotifyCanExecuteChanged();
        CancelAllCommand.NotifyCanExecuteChanged();

        var tasks = pending.Select(RunJobAsync).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            IsAnyRunning = Jobs.Any(j => j.IsRunning);
            StartAllCommand.NotifyCanExecuteChanged();
            CancelAllCommand.NotifyCanExecuteChanged();
        });
    }
    private bool CanStartAll() =>
        Jobs.Any(j => j.Status is JobStatus.Idle or JobStatus.Failed or JobStatus.Canceled);

    private async Task RunJobAsync(JobViewModel job)
    {
        await _slots.WaitAsync().ConfigureAwait(false);
        try
        {
            // 라이선스 한도 체크
            if (!_license.CanConvert(out var limitReason))
            {
                job.Status = JobStatus.Failed;
                job.StatusText = "한도 초과";
                job.ErrorMessage = limitReason;
                AppLogger.Instance.Warn($"[한도초과] {job.Url}");
                return;
            }

            using var cts = new CancellationTokenSource();
            job.Cts = cts;
            job.Status = JobStatus.Resolving;
            job.Progress = 0;
            job.StatusText = "준비 중...";
            job.ErrorMessage = null;

            var progress = new Progress<ConversionProgress>(p =>
            {
                job.Progress = p.Ratio;
                job.Status = p.Status;
                job.StatusText = p.Status switch
                {
                    JobStatus.Resolving => "스트림 해석 중",
                    JobStatus.Downloading => $"다운로드 {p.Ratio:P0}",
                    JobStatus.Muxing => "변환 중",
                    JobStatus.Completed => "완료",
                    _ => p.Status.ToString()
                };
                if (!string.IsNullOrEmpty(p.VideoTitle)) job.Title = p.VideoTitle!;
            });

            try
            {
                var result = await _downloadService.ConvertAsync(
                    job.Url.Trim(), job.Format, OutputFolder, progress, cts.Token).ConfigureAwait(false);
                job.OutputPath = result.OutputPath;
                job.Title = result.VideoTitle;
                job.Status = JobStatus.Completed;
                job.Progress = 1.0;
                job.StatusText = "완료";
                _license.RecordConversion();
                OnPropertyChanged(nameof(TierBadge));
                AppLogger.Instance.Info($"[완료] {result.VideoTitle}");
            }
            catch (OperationCanceledException)
            {
                job.Status = JobStatus.Canceled;
                job.StatusText = "취소됨";
                AppLogger.Instance.Warn($"[취소] {job.Url}");
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.StatusText = $"오류";
                job.ErrorMessage = ex.Message;
                AppLogger.Instance.Error($"[실패] {job.Url}", ex);
            }
            finally
            {
                job.Cts = null;
            }
        }
        finally
        {
            _slots.Release();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelAll))]
    private void CancelAll()
    {
        foreach (var j in Jobs.Where(j => j.IsRunning))
            j.Cts?.Cancel();
    }
    private bool CanCancelAll() => Jobs.Any(j => j.IsRunning);

    [RelayCommand(CanExecute = nameof(CanClearCompleted))]
    private void ClearCompleted()
    {
        var done = Jobs.Where(j => j.Status is JobStatus.Completed or JobStatus.Canceled or JobStatus.Failed).ToList();
        foreach (var j in done) Jobs.Remove(j);
    }
    private bool CanClearCompleted() =>
        Jobs.Any(j => j.Status is JobStatus.Completed or JobStatus.Canceled or JobStatus.Failed);

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
        catch (Exception ex) { AppLogger.Instance.Error("폴더 열기 실패", ex); }
    }

    [RelayCommand]
    private void Paste()
    {
        try
        {
            if (Clipboard.ContainsText())
                Url = string.IsNullOrEmpty(Url) ? Clipboard.GetText().Trim() : Url + Environment.NewLine + Clipboard.GetText().Trim();
        }
        catch (Exception ex) { AppLogger.Instance.Warn($"클립보드 읽기 실패: {ex.Message}"); }
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
