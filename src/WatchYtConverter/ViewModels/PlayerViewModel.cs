using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtConverter.App.Logging;
using YtConverter.App.Models;
using YtConverter.App.Services;
using WatchYtConverter.Services;

namespace WatchYtConverter.ViewModels;

/// <summary>
/// 목적: 유튜브로 영상을 보다가, 보고 있는 영상을 MP3 로 변환한다.
/// 변환은 백그라운드로 돌아 시청을 막지 않으며, 재생은 선택 사항이다.
/// </summary>
public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private const int MaxConcurrent = 3;

    private readonly IDownloadService _download;
    private readonly AudioPlayer _audio;
    private readonly PlayerSettings _settings;
    private readonly SemaphoreSlim _slots = new(MaxConcurrent, MaxConcurrent);

    private bool _seeking;

    public PlayerViewModel(IDownloadService download, AudioPlayer audio)
    {
        _download = download;
        _audio = audio;
        _settings = PlayerSettings.Load();

        _outputFolder = _settings.OutputFolder ?? PlayerSettings.DefaultOutputFolder;
        _instantConvertEnabled = _settings.InstantConvertEnabled;
        _audio.Volume = _settings.Volume;

        _audio.Tick += OnAudioTick;
        _audio.Ended += () => { PlayingStatus = "재생 완료"; OnAudioTick(); };
        _audio.Failed += msg => { PlayingStatus = "재생 실패"; ErrorText = msg; };
    }

    /// <summary>백그라운드 변환 목록.</summary>
    public ObservableCollection<ConversionJobViewModel> Jobs { get; } = new();

    // ---- 지금 보고 있는 영상 (WebView 가 알려준다) ----
    [ObservableProperty] private string? _watchUrl;
    [ObservableProperty] private string? _watchTitle;
    [ObservableProperty] private string? _watchVideoId;

    // ---- 재생 중인 트랙 (선택 사항) ----
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private string _playingTitle = "";
    [ObservableProperty] private string? _playingThumbnailUrl;
    [ObservableProperty] private string _playingStatus = "";
    [ObservableProperty] private bool _hasTrack;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private double _positionSeconds;
    [ObservableProperty] private double _durationSeconds;
    [ObservableProperty] private string _positionText = "0:00";
    [ObservableProperty] private string _durationText = "0:00";

    // ---- 설정 ----
    /// <summary>MP3 저장 폴더. 변경하면 즉시 저장된다.</summary>
    [ObservableProperty] private string _outputFolder;

    /// <summary>
    /// 썸네일을 누르는 즉시 변환을 건다. 기본은 꺼짐 —
    /// 평소에는 유튜브처럼 영상이 재생되고, 변환은 사용자가 버튼으로 고른다.
    /// </summary>
    [ObservableProperty] private bool _instantConvertEnabled;

    /// <summary>MP3 를 재생할 때만 유튜브 영상을 멈춘다. View 가 주입한다.</summary>
    public Action? PauseWebVideo { get; set; }

    /// <summary>폴더 선택 대화상자. View 가 주입한다.</summary>
    public Func<string, string?>? PickFolder { get; set; }

    public double Volume
    {
        get => _audio.Volume;
        set
        {
            _audio.Volume = value;
            _settings.Volume = _audio.Volume;
            _settings.Save();
            OnPropertyChanged();
        }
    }

    // Maximum 이 0 이면 Slider 의 채움 구간이 트랙 전체를 덮어 100% 로 보인다.
    // 트랙이 없을 때는 1 을 돌려 Value=0 이 빈 상태로 그려지게 한다.
    public double SliderMaximum => DurationSeconds > 0 ? DurationSeconds : 1;

    partial void OnDurationSecondsChanged(double value) => OnPropertyChanged(nameof(SliderMaximum));

    partial void OnInstantConvertEnabledChanged(bool value)
    {
        _settings.InstantConvertEnabled = value;
        _settings.Save();
    }

    partial void OnOutputFolderChanged(string value)
    {
        _settings.OutputFolder = value;
        _settings.Save();
        AppLogger.Instance.Info($"[Player] 저장 폴더 변경: {value}");
    }

    /// <summary>WebView 에서 보고 있는 영상이 바뀔 때 호출된다.</summary>
    public void SetCurrentVideo(string? url, string? title, string? videoId)
    {
        WatchUrl = url;
        WatchTitle = title;
        WatchVideoId = videoId;
    }

    partial void OnWatchUrlChanged(string? value) => ConvertCurrentCommand.NotifyCanExecuteChanged();

    private bool CanConvertCurrent() => !string.IsNullOrWhiteSpace(WatchUrl);

    /// <summary>지금 보고 있는 영상을 백그라운드로 MP3 변환한다. 영상 재생은 그대로 둔다.</summary>
    [RelayCommand(CanExecute = nameof(CanConvertCurrent))]
    private void ConvertCurrent()
    {
        if (string.IsNullOrWhiteSpace(WatchUrl)) return;
        Enqueue(WatchUrl!, WatchTitle, WatchVideoId);
    }

    /// <summary>변환 작업을 큐에 넣고 즉시 백그라운드로 돌린다.</summary>
    public void Enqueue(string url, string? title, string? videoId)
    {
        // 같은 영상이 이미 진행 중이면 중복으로 걸지 않는다.
        var existing = Jobs.FirstOrDefault(j => j.Url == url && !j.IsFailed);
        if (existing is not null)
        {
            existing.StatusText = existing.IsDone ? "이미 변환됨" : existing.StatusText;
            return;
        }

        var job = new ConversionJobViewModel(url, title, videoId)
        {
            DismissRequested = Dismiss,
            PlayRequested = PlayJob
        };
        Jobs.Insert(0, job);

        // 의도적으로 await 하지 않는다 — 시청을 막지 않는 것이 목적이다.
        _ = RunJobAsync(job);
    }

    private async Task RunJobAsync(ConversionJobViewModel job)
    {
        var cts = new CancellationTokenSource();
        job.Cts = cts;

        // ConfigureAwait(false) 를 쓰지 않는다 — 이후 코드가 바인딩 속성과
        // MediaPlayer 를 건드리므로 UI 스레드에 머물러야 한다.
        await _slots.WaitAsync(cts.Token);

        try
        {
            if (cts.IsCancellationRequested) return;

            job.StatusText = "준비 중...";
            AppLogger.Instance.Info($"[Player] 변환 시작: {job.Url}");

            var progress = new Progress<ConversionProgress>(p =>
            {
                job.Progress = p.Ratio;
                job.StatusText = p.Status switch
                {
                    JobStatus.Resolving => "영상 확인 중...",
                    JobStatus.Downloading => $"내려받는 중 {p.Ratio:P0}",
                    JobStatus.Muxing => "MP3 변환 중...",
                    _ => job.StatusText
                };
                if (!string.IsNullOrEmpty(p.VideoTitle)) job.Title = p.VideoTitle!;
            });

            var result = await _download.ConvertAsync(
                job.Url, OutputFormat.Mp3, OutputFolder, progress, cts.Token);

            job.Title = result.VideoTitle;
            job.OutputPath = result.OutputPath;
            job.Progress = 1;
            job.IsDone = true;
            job.StatusText = "변환 완료";
            AppLogger.Instance.Info($"[Player] 변환 완료: {result.OutputPath}");
        }
        catch (OperationCanceledException)
        {
            job.StatusText = "취소됨";
            job.IsFailed = true;
        }
        catch (Exception ex)
        {
            job.StatusText = "변환 실패";
            job.IsFailed = true;
            ErrorText = ex.Message;
            AppLogger.Instance.Error($"[Player] 변환 실패: {job.Url}", ex);
        }
        finally
        {
            job.Cts = null;
            cts.Dispose();
            _slots.Release();
        }
    }

    private void Dismiss(ConversionJobViewModel job)
    {
        job.Cts?.Cancel();
        Jobs.Remove(job);
    }

    /// <summary>완료된 MP3 를 재생한다. 이때만 유튜브 영상을 멈춘다.</summary>
    private void PlayJob(ConversionJobViewModel job)
    {
        if (job.OutputPath is null || !File.Exists(job.OutputPath)) return;

        PauseWebVideo?.Invoke();
        _audio.Stop();
        _audio.Open(job.OutputPath);
        _audio.Play();

        PlayingTitle = job.Title;
        PlayingThumbnailUrl = job.ThumbnailUrl;
        PlayingStatus = "재생 중";
        HasTrack = true;
        IsPlaying = true;
        ErrorText = null;
        AppLogger.Instance.Info($"[Player] 재생: {job.OutputPath}");
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (!HasTrack) return;
        _audio.TogglePlayPause();
        IsPlaying = _audio.IsPlaying;
        PlayingStatus = IsPlaying ? "재생 중" : "일시정지";
    }

    [RelayCommand]
    private void StopPlayback()
    {
        _audio.Stop();
        HasTrack = false;
        IsPlaying = false;
        PositionSeconds = 0;
        DurationSeconds = 0;
        PositionText = DurationText = "0:00";
        PlayingTitle = "";
        PlayingThumbnailUrl = null;
        PlayingStatus = "";
        ErrorText = null;
    }

    /// <summary>저장 폴더를 탐색기로 연다. 재생 중이면 그 파일을 선택한 상태로 연다.</summary>
    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(OutputFolder);

            var current = _audio.CurrentPath;
            if (current is not null && File.Exists(current))
            {
                // /select 뒤 경로는 따옴표로 감싸야 공백이 든 경로가 깨지지 않는다.
                var arg = "/select,\"" + current + "\"";
                Process.Start(new ProcessStartInfo("explorer.exe", arg) { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo(OutputFolder) { UseShellExecute = true });
            }

            AppLogger.Instance.Info($"[Player] 폴더 열기: {OutputFolder}");
        }
        catch (Exception ex)
        {
            ErrorText = $"폴더를 열지 못했습니다: {ex.Message}";
            AppLogger.Instance.Error("[Player] 폴더 열기 실패", ex);
        }
    }

    [RelayCommand]
    private void ChangeFolder()
    {
        if (PickFolder is null) return;

        var picked = PickFolder(OutputFolder);
        if (string.IsNullOrWhiteSpace(picked)) return;

        try
        {
            Directory.CreateDirectory(picked);
            OutputFolder = picked;
            ErrorText = null;
        }
        catch (Exception ex)
        {
            ErrorText = $"폴더를 사용할 수 없습니다: {ex.Message}";
            AppLogger.Instance.Error("[Player] 폴더 변경 실패", ex);
        }
    }

    /// <summary>슬라이더 드래그 시작/종료. 드래그 중에는 타이머가 값을 덮어쓰지 않게 한다.</summary>
    public void BeginSeek() => _seeking = true;

    public void EndSeek(double seconds)
    {
        _seeking = false;
        if (!HasTrack) return;
        _audio.Position = TimeSpan.FromSeconds(seconds);
        OnAudioTick();
    }

    private void OnAudioTick()
    {
        var dur = _audio.Duration;
        DurationSeconds = dur.TotalSeconds;
        DurationText = Format(dur);

        if (!_seeking)
        {
            var pos = _audio.Position;
            PositionSeconds = pos.TotalSeconds;
            PositionText = Format(pos);
        }

        IsPlaying = _audio.IsPlaying;
    }

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
                          : $"{t.Minutes}:{t.Seconds:D2}";

    public void Dispose()
    {
        foreach (var j in Jobs) j.Cts?.Cancel();
        _audio.Dispose();
        _slots.Dispose();
    }
}
