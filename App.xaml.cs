using System;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using XamppVHostManager.Services;
using XamppVHostManager.ViewModels;

namespace XamppVHostManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Globalna obsługa nieobsłużonych wyjątków — czytelny komunikat zamiast crasha.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        // ===== Composition root: ręczne złożenie serwisów (bez kontenera DI) =====
        // DriveService najpierw — wykrywa literę dysku, na której leży .exe.
        var drive = new DriveService();
        var backup = new BackupService(drive);
        var configService = new ConfigService(drive);
        var vhostService = new VHostFileService(drive, backup);
        var hostsService = new HostsFileService(backup);
        var apacheService = new ApacheService();
        var syncService = new SyncService(drive, vhostService, hostsService);

        var vm = new MainViewModel(drive, configService, vhostService,
                                   hostsService, apacheService, syncService);

        var window = new MainWindow(vm);
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "Wystąpił nieoczekiwany błąd:\n\n" + e.Exception.Message,
            "AOWcloud Xampp — błąd",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>Czy proces ma uprawnienia administratora (potrzebne do hosts + restart Apache)?</summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
