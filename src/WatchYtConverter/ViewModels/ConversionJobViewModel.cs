using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WatchYtConverter.ViewModels;

/// <summary>
/// 백그라운드로 도는 변환 작업 한 건. 영상 시청을 막지 않는 것이 목적이므로
/// 완료돼도 자동 재생하지 않고, 재생은 사용자가 ▶ 로 고른다.
/// </summary>
public partial class ConversionJobViewModel : ObservableObject
{
    public ConversionJobViewModel(string url, string? title, string? videoId)
    {
        Url = url;
        VideoId = videoId;
        _title = string.IsNullOrWhiteSpace(title) ? url : title!;
        ThumbnailUrl = videoId is null ? null : $"https://i.ytimg.com/vi/{videoId}/default.jpg";
    }

    public string Url { get; }
    public string? VideoId { get; }
    public string? ThumbnailUrl { get; }

    internal CancellationTokenSource? Cts { get; set; }

    [ObservableProperty] private string _title;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusText = "대기 중";
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _isFailed;
    [ObservableProperty] private string? _outputPath;

    /// <summary>진행 중이면 취소, 끝났으면 목록에서 제거 — 둘 다 ✕ 하나로 처리한다.</summary>
    public Action<ConversionJobViewModel>? DismissRequested { get; set; }
    public Action<ConversionJobViewModel>? PlayRequested { get; set; }

    [RelayCommand]
    private void Dismiss() => DismissRequested?.Invoke(this);

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play() => PlayRequested?.Invoke(this);

    private bool CanPlay() => IsDone && !string.IsNullOrEmpty(OutputPath);

    partial void OnIsDoneChanged(bool value) => PlayCommand.NotifyCanExecuteChanged();
    partial void OnOutputPathChanged(string? value) => PlayCommand.NotifyCanExecuteChanged();
}
