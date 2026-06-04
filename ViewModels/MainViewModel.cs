using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using XamppVHostManager.Models;
using XamppVHostManager.Services;

namespace XamppVHostManager.ViewModels;

/// <summary>
/// Główny ViewModel okna. Trzyma config, listę projektów i wystawia operacje
/// (synchronizacja, zapis i zastosowanie, test, restart). Interakcje UI
/// (dialogi, potwierdzenia) realizuje code-behind okna, wołając te metody.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly DriveService _drive;
    private readonly ConfigService _configService;
    private readonly VHostFileService _vhostService;
    private readonly HostsFileService _hostsService;
    private readonly ApacheService _apacheService;
    private readonly SyncService _syncService;

    public AppConfig Config { get; private set; }

    public ObservableCollection<ProjectRowViewModel> Projects { get; } = new();

    private ProjectRowViewModel? _selected;
    public ProjectRowViewModel? SelectedProject
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    private string _bannerText = "Inicjalizacja…";
    public string BannerText { get => _bannerText; private set => SetField(ref _bannerText, value); }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }

    private bool _hasWarnings;
    public bool HasWarnings { get => _hasWarnings; private set => SetField(ref _hasWarnings, value); }

    private string _warningsText = "";
    public string WarningsText { get => _warningsText; private set => SetField(ref _warningsText, value); }

    private bool _isAdmin;
    public bool IsAdmin { get => _isAdmin; private set => SetField(ref _isAdmin, value); }

    /// <summary>Czy ostatnia synchronizacja coś zmieniła (sugeruj restart Apache).</summary>
    public bool ChangesPending { get; private set; }

    public DriveService Drive => _drive;

    public MainViewModel(
        DriveService drive,
        ConfigService configService,
        VHostFileService vhostService,
        HostsFileService hostsService,
        ApacheService apacheService,
        SyncService syncService)
    {
        _drive = drive;
        _configService = configService;
        _vhostService = vhostService;
        _hostsService = hostsService;
        _apacheService = apacheService;
        _syncService = syncService;

        Config = new AppConfig();
        IsAdmin = App.IsRunningAsAdmin();
    }

    /// <summary>Wczytuje config z dysku i odświeża listę w UI.</summary>
    public void LoadConfig()
    {
        Config = _configService.Load();
        RebuildProjectList();
    }

    private void RebuildProjectList()
    {
        Projects.Clear();
        foreach (var p in Config.Projects.OrderBy(p => p.ServerName, StringComparer.OrdinalIgnoreCase))
        {
            var row = new ProjectRowViewModel(p, _drive);
            // Przełączenie Enabled w gridzie -> natychmiastowy zapis i zastosowanie.
            row.EnabledToggled += () => OnEnabledToggled();
            Projects.Add(row);
        }
    }

    private void OnEnabledToggled()
    {
        // Zapisz config i zregeneruj pliki — wynik przekazujemy przez zdarzenie do widoku.
        var result = SaveAndApply();
        ApplyAndAnnounceRequested?.Invoke(result);
    }

    /// <summary>
    /// SEKWENCJA STARTOWA / „Synchronizuj teraz": regeneracja vhosts + reconcile hosts.
    /// </summary>
    public SyncResult RunStartupSync()
    {
        var result = _syncService.Run(Config);
        BannerText = result.BannerText;
        ChangesPending = result.AnythingChanged;
        SetWarnings(result);
        StatusText = BuildSyncStatus(result);
        return result;
    }

    /// <summary>
    /// „Zapisz i zastosuj": zapis config.json + regeneracja vhosts.conf + reconcile hosts (+ .bak).
    /// </summary>
    public SyncResult SaveAndApply()
    {
        // Zapis listy do config.json (źródło prawdy na dysku).
        _configService.Save(Config);
        // Pełna synchronizacja plików hosta.
        var result = _syncService.Run(Config);
        BannerText = result.BannerText;
        ChangesPending = result.AnythingChanged;
        SetWarnings(result);
        StatusText = "Zapisano i zastosowano. " + BuildSyncStatus(result);
        return result;
    }

    /// <summary>Dodaje nowy projekt i zapisuje.</summary>
    public SyncResult AddProject(ProjectModel model)
    {
        Config.Projects.Add(model);
        RebuildProjectList();
        return SaveAndApply();
    }

    /// <summary>Zastępuje istniejący projekt edytowaną wersją i zapisuje.</summary>
    public SyncResult UpdateProject(ProjectModel original, ProjectModel edited)
    {
        var idx = Config.Projects.IndexOf(original);
        if (idx >= 0)
            Config.Projects[idx] = edited;
        RebuildProjectList();
        return SaveAndApply();
    }

    /// <summary>Usuwa projekt i zapisuje.</summary>
    public SyncResult RemoveProject(ProjectModel model)
    {
        Config.Projects.Remove(model);
        RebuildProjectList();
        return SaveAndApply();
    }

    /// <summary>Test konfiguracji Apache (httpd.exe -t). NIE restartuje.</summary>
    public Task<ProcessResult> TestConfigAsync()
    {
        var httpd = _drive.GetHttpdExePath(Config.Settings.HttpdExeRelative);
        return _apacheService.TestConfigAsync(httpd);
    }

    /// <summary>Restart Apache (httpd.exe -k restart).</summary>
    public async Task<ProcessResult> RestartApacheAsync()
    {
        var httpd = _drive.GetHttpdExePath(Config.Settings.HttpdExeRelative);
        var r = await _apacheService.RestartAsync(httpd);
        if (r.Success)
        {
            ChangesPending = false;
            StatusText = "Apache zrestartowany.";
        }
        return r;
    }

    /// <summary>URL projektu do otwarcia w przeglądarce (uwzględnia port).</summary>
    public string GetProjectUrl(ProjectModel p)
    {
        var scheme = "http";
        return p.Port == 80
            ? $"{scheme}://{p.ServerName}/"
            : $"{scheme}://{p.ServerName}:{p.Port}/";
    }


    private void SetWarnings(SyncResult result)
    {
        var warnings = result.Warnings.ToList();
        if (!IsAdmin)
            warnings.Insert(0, "Program nie działa jako administrator — edycja hosts i restart Apache mogą się nie powieść.");

        HasWarnings = warnings.Count > 0;
        WarningsText = string.Join("\n", warnings.Select(w => "• " + w));
    }

    private static string BuildSyncStatus(SyncResult r)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (r.HostsAdded.Count > 0) parts.Add($"dodano do hosts: {string.Join(", ", r.HostsAdded)}");
        if (r.HostsRemoved.Count > 0) parts.Add($"usunięto z hosts: {string.Join(", ", r.HostsRemoved)}");
        if (r.VHostsChanged) parts.Add("zregenerowano vhosts.conf");
        if (parts.Count == 0) parts.Add("brak zmian w plikach");
        return string.Join("; ", parts) + ".";
    }

    /// <summary>Zdarzenie: po przełączeniu Enabled w gridzie widok ma ogłosić wynik.</summary>
    public event Action<SyncResult>? ApplyAndAnnounceRequested;
}
