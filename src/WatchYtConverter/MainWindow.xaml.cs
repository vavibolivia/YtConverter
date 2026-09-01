using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using YtConverter.App.Logging;
using YtConverter.App.Services;
using WatchYtConverter.Services;
using WatchYtConverter.ViewModels;

namespace WatchYtConverter;

public partial class MainWindow : Window
{
    private readonly PlayerViewModel _vm;

    // JS 는 camelCase 로 보내므로 대소문자를 무시하고 매핑한다.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MainWindow()
    {
        InitializeComponent();

        _vm = new PlayerViewModel(new DownloadService(new FfmpegProvisioner()), new AudioPlayer())
        {
            PickFolder = PickFolder,
            PauseWebVideo = PauseWebVideo
        };
        DataContext = _vm;
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        Loaded += OnLoaded;
        Closed += (_, _) => _vm.Dispose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 로그인 세션이 유지되도록 사용자 데이터 폴더를 고정한다.
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YtConverter", "webview2");
            Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await Web.EnsureCoreWebView2Async(env);

            var core = Web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreDevToolsEnabled = false;

            // SPA 전환에도 유지되도록 문서 생성 시점에 주입한다.
            await core.AddScriptToExecuteOnDocumentCreatedAsync(YouTubeBridge.BridgeScript);

            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationCompleted += OnNavigationCompleted;

            // 새 창 요청은 같은 창에서 처리한다.
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                core.Navigate(args.Uri);
            };

            core.Navigate(YouTubeBridge.HomeUrl);
            AppLogger.Instance.Info("[Player] WebView2 초기화 완료");
        }
        catch (Exception ex)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            AppLogger.Instance.Error("[Player] WebView2 초기화 실패", ex);
            MessageBox.Show(
                "WebView2 를 초기화하지 못했습니다.\n\n" + ex.Message +
                "\n\nMicrosoft Edge WebView2 Runtime 이 설치되어 있는지 확인하세요.",
                "watchYTConverter", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Collapsed;
        AppLogger.Instance.Info($"[Player] 이동 완료: {Web.Source} (성공={e.IsSuccess})");
        await PushInstantConvertAsync();
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.InstantConvertEnabled))
            await PushInstantConvertAsync();
    }

    private async Task PushInstantConvertAsync()
    {
        if (Web.CoreWebView2 is null) return;
        try
        {
            await Web.CoreWebView2.ExecuteScriptAsync(
                YouTubeBridge.SetInstantConvertScript(_vm.InstantConvertEnabled));
        }
        catch (Exception ex)
        {
            AppLogger.Instance.Warn($"[Player] 바로 변환 설정 전달 실패: {ex.Message}");
        }
    }

    private async void PauseWebVideo()
    {
        if (Web.CoreWebView2 is null) return;
        try { await Web.CoreWebView2.ExecuteScriptAsync(YouTubeBridge.PauseVideoScript); }
        catch (Exception ex) { AppLogger.Instance.Warn($"[Player] 영상 일시정지 실패: {ex.Message}"); }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw;
        try { raw = e.TryGetWebMessageAsString(); }
        catch { return; }

        BridgeMessage? msg;
        try { msg = JsonSerializer.Deserialize<BridgeMessage>(raw, JsonOpts); }
        catch (JsonException ex)
        {
            AppLogger.Instance.Warn($"[Player] 메시지 파싱 실패: {ex.Message}");
            return;
        }

        if (msg is null) return;

        switch (msg.Type)
        {
            // 보고 있는 영상이 바뀜 → '이 영상 MP3 로' 버튼 대상 갱신
            case "nav":
                _vm.SetCurrentVideo(msg.Url, msg.Title, msg.VideoId);
                break;

            // '썸네일 바로 변환' 이 켜진 상태에서 썸네일을 누름 → 백그라운드 변환만 건다
            case "play" when !string.IsNullOrWhiteSpace(msg.Url):
                _vm.Enqueue(msg.Url!, msg.Title, msg.VideoId);
                break;
        }
    }

    /// <summary>폴더 선택 대화상자. .NET 8 WPF 의 OpenFolderDialog 를 쓴다.</summary>
    private string? PickFolder(string current)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "MP3 저장 폴더 선택",
            Multiselect = false
        };

        try { if (Directory.Exists(current)) dlg.InitialDirectory = current; }
        catch { /* 초기 경로는 없어도 무방 */ }

        return dlg.ShowDialog(this) == true ? dlg.FolderName : null;
    }

    private void Seek_DragStarted(object sender, DragStartedEventArgs e) => _vm.BeginSeek();

    private void Seek_DragCompleted(object sender, DragCompletedEventArgs e) =>
        _vm.EndSeek(Seek.Value);

    // IsMoveToPointEnabled 로 트랙을 눌러 이동한 경우도 반영한다.
    private void Seek_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Slider s) _vm.EndSeek(s.Value);
    }

    private sealed class BridgeMessage
    {
        public string? Type { get; set; }
        public string? VideoId { get; set; }
        public string? Title { get; set; }
        public string? Url { get; set; }
    }
}
