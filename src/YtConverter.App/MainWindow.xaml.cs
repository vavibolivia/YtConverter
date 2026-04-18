using System.Windows;
using YtConverter.App.Services;
using YtConverter.App.ViewModels;

namespace YtConverter.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var ffmpeg = new FfmpegProvisioner();
        var downloader = new DownloadService(ffmpeg);
        var license = new LicenseService();
        DataContext = new MainViewModel(downloader, license);
    }
}
