using System.Windows;
using System.Windows.Threading;
using YtConverter.App.Logging;

namespace WatchYtConverter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // YtConverter.App 이 겪었던 문제 재발 방지: 처리되지 않은 예외 하나로
        // 프로세스가 통째로 죽지 않도록 UI 스레드 예외를 잡아 로그로 남긴다.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLogger.Instance.Error("치명적 예외", args.ExceptionObject as Exception);

        AppLogger.Instance.Info("Player 시작");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLogger.Instance.Error("UI 스레드 예외", e.Exception);
        MessageBox.Show(e.Exception.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }
}
