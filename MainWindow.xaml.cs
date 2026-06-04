using System;
using System.Diagnostics;
using System.Windows;
using XamppVHostManager.Models;
using XamppVHostManager.Services;
using XamppVHostManager.ViewModels;
using XamppVHostManager.Views;

namespace XamppVHostManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // Po przełączeniu Enabled w gridzie VM zgłasza wynik -> ogłoś użytkownikowi.
        _vm.ApplyAndAnnounceRequested += AnnounceSyncResult;

        Loaded += MainWindow_Loaded;
    }

    /// <summary>
    /// SEKWENCJA STARTOWA: wczytaj config -> synchronizuj (regeneruj vhosts + reconcile hosts)
    /// -> pokaż baner -> jeśli coś się zmieniło, zaproponuj restart Apache (z pytaniem).
    /// </summary>
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_vm.IsAdmin)
        {
            MessageBox.Show(this,
                "Program nie został uruchomiony jako administrator.\n\n" +
                "Edycja pliku hosts (C:\\Windows\\System32\\drivers\\etc\\hosts) oraz restart Apache " +
                "wymagają uprawnień administratora. Zamknij program i uruchom go ponownie " +
                "(prawy przycisk → Uruchom jako administrator).",
                "Brak uprawnień administratora",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        try
        {
            _vm.LoadConfig();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Błąd wczytywania config.json",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        var result = _vm.RunStartupSync();

        if (!result.XamppFound)
        {
            MessageBox.Show(this,
                $"Nie znaleziono XAMPP pod ścieżką:\n{_vm.Drive.GetXamppRootPath()}\n\n" +
                "Sprawdź, czy dysk zawiera katalog 'xampp', lub ustaw ścieżki w Ustawieniach.",
                "Nie znaleziono XAMPP", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        // Jeśli sekwencja startowa coś zmieniła — zapytaj o restart Apache.
        if (result.AnythingChanged)
            await MaybeRestartAsync("Synchronizacja zmieniła konfigurację.");
    }

    private void AnnounceSyncResult(SyncResult result)
    {
        // Po toggle Enabled — ciche zastosowanie; baner/status już zaktualizowane przez VM.
    }

    // ===================== Lista: Dodaj / Edytuj / Usuń / Otwórz =====================

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var vm = new ProjectEditViewModel(_vm.Drive, null);
        var dlg = new ProjectEditDialog(vm) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var result = _vm.AddProject(vm.ToModel());
            AfterChange(result);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void Grid_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (_vm.SelectedProject is not { } row)
        {
            Info("Zaznacz projekt do edycji.");
            return;
        }

        var original = row.Model;
        var vm = new ProjectEditViewModel(_vm.Drive, original);
        var dlg = new ProjectEditDialog(vm) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var result = _vm.UpdateProject(original, vm.ToModel());
            AfterChange(result);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProject is not { } row)
        {
            Info("Zaznacz projekt do usunięcia.");
            return;
        }

        var confirm = MessageBox.Show(this,
            $"Usunąć projekt '{row.ServerName}'?\n\nWpis zostanie usunięty z vhosts.conf i z pliku hosts.",
            "Potwierdź usunięcie", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm == MessageBoxResult.Yes)
        {
            var result = _vm.RemoveProject(row.Model);
            AfterChange(result);
        }
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProject is not { } row)
        {
            Info("Zaznacz projekt do otwarcia.");
            return;
        }
        var url = _vm.GetProjectUrl(row.Model);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Info("Nie udało się otworzyć przeglądarki: " + ex.Message);
        }
    }

    // ===================== Operacje globalne =====================

    private async void SaveApply_Click(object sender, RoutedEventArgs e)
    {
        var result = _vm.SaveAndApply();
        await AfterChangeAsync(result);
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        var result = _vm.RunStartupSync();
        await AfterChangeAsync(result);
    }

    private async void TestConfig_Click(object sender, RoutedEventArgs e)
    {
        var r = await _vm.TestConfigAsync();
        var caption = r.Success ? "Test konfiguracji — OK" : "Test konfiguracji — BŁĄD";
        var icon = r.Success ? MessageBoxImage.Information : MessageBoxImage.Error;
        var text = string.IsNullOrWhiteSpace(r.CombinedOutput)
            ? (r.Success ? "Syntax OK" : "httpd.exe zwrócił błąd bez treści.")
            : r.CombinedOutput;

        if (!r.Success)
            text += "\n\nNIE restartuję Apache. Możesz przywrócić kopię .bak z katalogu backups na dysku.";

        MessageBox.Show(this, text, caption, MessageBoxButton.OK, icon);
    }

    private async void Restart_Click(object sender, RoutedEventArgs e)
    {
        await DoRestartAsync();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsDialog(_vm.Config.Settings.Clone(), _vm.Drive) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            // Zapisz nowe ustawienia i zastosuj.
            _vm.Config.Settings = dlg.Result;
            var result = _vm.SaveAndApply();
            AfterChange(result);
        }
    }

    // ===================== Pomocnicze =====================

    private async System.Threading.Tasks.Task MaybeRestartAsync(string reason)
    {
        if (!_vm.Config.Settings.AskBeforeRestart)
            return;

        var ask = MessageBox.Show(this,
            reason + "\n\nZrestartować Apache teraz, aby zmiany zaczęły działać?",
            "Restart Apache?", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (ask == MessageBoxResult.Yes)
            await DoRestartAsync();
    }

    private async System.Threading.Tasks.Task DoRestartAsync()
    {
        var r = await _vm.RestartApacheAsync();
        if (r.Success)
        {
            MessageBox.Show(this, "Apache zrestartowany.", "Restart Apache",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(this,
                "Nie udało się zrestartować Apache:\n\n" + r.CombinedOutput,
                "Restart Apache — błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AfterChange(SyncResult result)
    {
        if (result.AnythingChanged && _vm.Config.Settings.AskBeforeRestart)
            _ = MaybeRestartAsync("Zmiany zostały zapisane i zastosowane.");
    }

    private async System.Threading.Tasks.Task AfterChangeAsync(SyncResult result)
    {
        if (result.AnythingChanged)
            await MaybeRestartAsync("Zmiany zostały zastosowane.");
    }

    private void Info(string message) =>
        MessageBox.Show(this, message, "AOWcloud Xampp", MessageBoxButton.OK, MessageBoxImage.Information);
}
