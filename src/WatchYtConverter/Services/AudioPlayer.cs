using System.Windows.Media;
using System.Windows.Threading;
using YtConverter.App.Logging;

namespace WatchYtConverter.Services;

/// <summary>
/// MediaPlayer 래퍼. WPF MediaPlayer 는 UI 스레드에서 생성·조작해야 하므로
/// 이 클래스의 모든 멤버는 UI 스레드 전용이다.
/// </summary>
public sealed class AudioPlayer : IDisposable
{
    private readonly MediaPlayer _player = new();
    private readonly DispatcherTimer _ticker;
    private bool _isPlaying;
    private bool _hasMedia;

    public event Action? Tick;
    public event Action? Ended;
    public event Action<string>? Failed;

    public AudioPlayer()
    {
        _player.MediaOpened += (_, _) => { _hasMedia = true; Tick?.Invoke(); };
        _player.MediaEnded += (_, _) =>
        {
            _isPlaying = false;
            _player.Stop();
            Ended?.Invoke();
        };
        _player.MediaFailed += (_, e) =>
        {
            _isPlaying = false;
            _hasMedia = false;
            AppLogger.Instance.Error("재생 실패", e.ErrorException);
            Failed?.Invoke(e.ErrorException?.Message ?? "재생할 수 없는 파일입니다.");
        };

        _ticker = new DispatcherTimer(DispatcherPriority.Normal)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _ticker.Tick += (_, _) => Tick?.Invoke();
    }

    public bool IsPlaying => _isPlaying;
    public bool HasMedia => _hasMedia;
    public string? CurrentPath { get; private set; }

    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Position = value;
    }

    public TimeSpan Duration =>
        _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;

    public double Volume
    {
        get => _player.Volume;
        set => _player.Volume = Math.Clamp(value, 0, 1);
    }

    public void Open(string path)
    {
        _hasMedia = false;
        CurrentPath = path;
        _player.Open(new Uri(path, UriKind.Absolute));
    }

    public void Play()
    {
        if (CurrentPath is null) return;
        _player.Play();
        _isPlaying = true;
        _ticker.Start();
        Tick?.Invoke();
    }

    public void Pause()
    {
        _player.Pause();
        _isPlaying = false;
        _ticker.Stop();
        Tick?.Invoke();
    }

    public void TogglePlayPause()
    {
        if (_isPlaying) Pause(); else Play();
    }

    public void Stop()
    {
        _player.Stop();
        _player.Close();
        _isPlaying = false;
        _hasMedia = false;
        CurrentPath = null;
        _ticker.Stop();
        Tick?.Invoke();
    }

    public void Dispose()
    {
        _ticker.Stop();
        _player.Close();
    }
}
