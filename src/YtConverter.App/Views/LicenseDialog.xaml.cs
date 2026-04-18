using System.Windows;
using YtConverter.App.Services;

namespace YtConverter.App.Views;

public partial class LicenseDialog : Window
{
    private readonly LicenseService _svc;

    public LicenseDialog(LicenseService svc)
    {
        InitializeComponent();
        _svc = svc;
        Refresh();
    }

    private void Refresh()
    {
        TierText.Text = _svc.TierBadge;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_svc.ApplyKey(KeyBox.Text, out var msg))
        {
            MessageBox.Show(msg, "성공", MessageBoxButton.OK, MessageBoxImage.Information);
            Refresh();
        }
        else
        {
            MessageBox.Show(msg, "키 적용 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void MockPurchase_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "모의 결제를 진행합니다.\n\n실제 결제는 연동되지 않았으며, 데모용 Pro 키가 발급됩니다.\n계속하시겠습니까?",
            "구매 (모의)", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (result != MessageBoxResult.OK) return;

        var key = _svc.IssueMockProKey();
        MessageBox.Show(
            $"Pro 라이선스가 발급되었습니다.\n\n키: {key}\n\n영수증은 이메일로 발송되었습니다 (모의).",
            "결제 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        Refresh();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
