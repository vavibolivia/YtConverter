using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using YtConverter.App.Services;
using YtConverter.App.ViewModels;

namespace YtConverter.App;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext;
    private static readonly Regex YtUrlRe = new(
        @"(https?://)?(www\.|m\.|music\.)?(youtube\.com/|youtu\.be/)\S+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MainWindow()
    {
        InitializeComponent();
        var ffmpeg = new FfmpegProvisioner();
        var downloader = new DownloadService(ffmpeg);
        DataContext = new MainViewModel(downloader);
    }

    private void UrlBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (Vm.AddCommand.CanExecute(null)) Vm.AddCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (HasDroppableUrl(e))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Visibility = Visibility.Visible;
        }
        else e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = HasDroppableUrl(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;
        var text = ExtractDroppedText(e);
        if (string.IsNullOrWhiteSpace(text)) return;
        var urls = YtUrlRe.Matches(text).Select(m => m.Value).Distinct().ToArray();
        if (urls.Length == 0) return;
        Vm.Url = string.Join(Environment.NewLine, urls);
        if (Vm.AddCommand.CanExecute(null)) Vm.AddCommand.Execute(null);
    }

    private static bool HasDroppableUrl(DragEventArgs e)
    {
        var text = ExtractDroppedText(e);
        return !string.IsNullOrWhiteSpace(text) && YtUrlRe.IsMatch(text);
    }

    private static string? ExtractDroppedText(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.UnicodeText))
            return e.Data.GetData(DataFormats.UnicodeText) as string;
        if (e.Data.GetDataPresent(DataFormats.Text))
            return e.Data.GetData(DataFormats.Text) as string;
        if (e.Data.GetDataPresent("UniformResourceLocatorW"))
        {
            var ms = e.Data.GetData("UniformResourceLocatorW") as System.IO.Stream;
            if (ms is not null)
            {
                using var reader = new System.IO.StreamReader(ms, System.Text.Encoding.Unicode);
                return reader.ReadToEnd();
            }
        }
        return null;
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        Vm?.CheckClipboardForUrl();
    }
}
