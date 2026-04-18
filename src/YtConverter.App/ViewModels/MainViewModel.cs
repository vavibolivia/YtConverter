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
    private readonly QueueStore _queue;
    private SemaphoreSlim _slots;
    private bool _suspendSave;

    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private OutputFormat _selectedFormat = OutputFormat.Mp3;
    [ObservableProperty] private string _outputFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Music", "YtConverter");
    [ObservableProperty] private int _maxConcurrency = 3;

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
        var allowed = Math.Max(1, value);
        _slots = new SemaphoreSlim(allowed, allowed);
        AppLogger.Instance.Info($"동시 작업 수 변경: {allowed}");
    }

    public MainViewModel(IDownloadService downloadService)
    {
        _downloadService = downloadService;
        _queue = new QueueStore();
        _slots = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);
        AppLogger.Instance.AttachSink(AppendLog);
        AppLogger.Instance.Info("앱 시작");
        Directory.CreateDirectory(OutputFolder);

        // 저장된 큐 복원
        _suspendSave = true;
        try
        {
            var restored = _queue.Load();
            int resumeCount = 0;
            foreach (var snap in restored)
            {
                var vm = JobViewModel.FromSnapshot(snap);
                vm.RequestRemove = j => Jobs.Remove(j);
                vm.StateChanged = PersistQueue;
                Jobs.Add(vm);
                if (vm.Status == JobStatus.Idle && !string.IsNullOrEmpty(vm.Url)) resumeCount++;
            }
            if (restored.Count > 0)
                AppLogger.Instance.Info($"이전 큐 복원: 총 {restored.Count}건 (재개 대상 {resumeCount}건)");
        }
        finally { _suspendSave = false; }

        Jobs.CollectionChanged += (_, __) =>
        {
            StartAllCommand.NotifyCanExecuteChanged();
            CancelAllCommand.NotifyCanExecuteChanged();
            ClearCompletedCommand.NotifyCanExecuteChanged();
            PersistQueue();
        };

        // 앱 시작 직후 재개할 작업이 있으면 자동 시작
        if (Jobs.Any(j => j.Status == JobStatus.Idle))
        {
            Application.Current?.Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(400);
                AppLogger.Instance.Info("미완료 작업 자동 재개");
                if (StartAllCommand.CanExecute(null)) await StartAllCommand.ExecuteAsync(null);
            });
        }
    }

    private void PersistQueue()
    {
        if (_suspendSave) return;
        _queue.Save(Jobs.Select(j => j.ToSnapshot()));
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        if (string.IsNullOrWhiteSpace(Url)) return;
        var urls = Url.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var u in urls)
        {
            var job = new JobViewModel { Url = u, Format = SelectedFormat };
            job.RequestRemove = j => Jobs.Remove(j);
            job.StateChanged = PersistQueue;
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

        StartAllCommand.NotifyCanExecuteChanged();
        CancelAllCommand.NotifyCanExecuteChanged();

        var tasks = pending.Select(RunJobAsync).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
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
                job.StatusText = "오류";
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
