using System.Windows;
using XamppVHostManager.Models;
using XamppVHostManager.Services;

namespace XamppVHostManager.Views;

public partial class SettingsDialog : Window
{
    private readonly DriveService _drive;
    private readonly int _httpsPort;
    public AppSettings Result { get; private set; }

    public SettingsDialog(AppSettings settings, DriveService drive)
    {
        InitializeComponent();
        _drive = drive;
        Result = settings;
        _httpsPort = settings.HttpsPort <= 0 ? 443 : settings.HttpsPort;

        VHostsBox.Text = settings.VHostsConfRelative;
        HttpdBox.Text = settings.HttpdExeRelative;
        HostsBox.Text = settings.HostsPathOverride;
        AskRestartBox.IsChecked = settings.AskBeforeRestart;
        HttpsBox.IsChecked = settings.GenerateHttps;
        SslCertBox.Text = settings.SslCertFile;
        SslKeyBox.Text = settings.SslCertKeyFile;

        VHostsBox.TextChanged += (_, _) => UpdatePreviews();
        HttpdBox.TextChanged += (_, _) => UpdatePreviews();
        UpdatePreviews();
    }

    private void UpdatePreviews()
    {
        VHostsPreview.Text = "→ " + _drive.GetVHostsConfPath(VHostsBox.Text);
        HttpdPreview.Text = "→ " + _drive.GetHttpdExePath(HttpdBox.Text);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = new AppSettings
        {
            VHostsConfRelative = VHostsBox.Text.Trim(),
            HttpdExeRelative = HttpdBox.Text.Trim(),
            HostsPathOverride = HostsBox.Text.Trim(),
            AskBeforeRestart = AskRestartBox.IsChecked == true,
            GenerateHttps = HttpsBox.IsChecked == true,
            HttpsPort = _httpsPort,
            SslCertFile = SslCertBox.Text.Trim(),
            SslCertKeyFile = SslKeyBox.Text.Trim()
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
