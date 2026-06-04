using System;
using System.Collections.Generic;
using XamppVHostManager.Models;

namespace XamppVHostManager.Services;

/// <summary>Wynik sekwencji synchronizacji — zasila baner i decyzję o restarcie.</summary>
public sealed class SyncResult
{
    public string DriveLetter { get; init; } = "";
    public string MachineName { get; init; } = "";
    public int SyncedProjects { get; init; }
    public bool VHostsChanged { get; init; }
    public bool HostsChanged { get; init; }
    public List<string> HostsAdded { get; init; } = new();
    public List<string> HostsRemoved { get; init; } = new();
    public bool XamppFound { get; init; }
    public List<string> Warnings { get; init; } = new();

    /// <summary>Czy cokolwiek się zmieniło — wtedy warto zaproponować restart Apache.</summary>
    public bool AnythingChanged => VHostsChanged || HostsChanged;

    /// <summary>Tekst banera: "Wykryto dysk: E: | Host: DESKTOP-Y | Zsynchronizowano N projektów".</summary>
    public string BannerText =>
        $"Wykryto dysk: {DriveLetter} | Host: {MachineName} | Zsynchronizowano {SyncedProjects} projektów";
}

/// <summary>
/// Orkiestruje SEKWENCJĘ STARTOWĄ (i ręczne „Synchronizuj teraz"):
/// 1. (litera dysku już wykryta w DriveService)
/// 2. wczytaj config.json,
/// 3. rozwiń ścieżki względne (robi się przy generowaniu),
/// 4. zregeneruj sekcję vhostów z aktualną literą,
/// 5. reconcile lokalnego hosts,
/// 6. zwróć dane do banera + flagę „coś się zmieniło".
/// </summary>
public sealed class SyncService
{
    private readonly DriveService _drive;
    private readonly VHostFileService _vhosts;
    private readonly HostsFileService _hosts;

    public SyncService(DriveService drive, VHostFileService vhosts, HostsFileService hosts)
    {
        _drive = drive;
        _vhosts = vhosts;
        _hosts = hosts;
    }

    /// <summary>
    /// Wykonuje pełną synchronizację dla podanego configu.
    /// Wyjątki I/O łapiemy do listy ostrzeżeń, żeby aplikacja wstała nawet,
    /// gdy np. brak uprawnień do hosts.
    /// </summary>
    public SyncResult Run(AppConfig config)
    {
        var warnings = new List<string>();
        var xamppFound = _drive.XamppExists();
        if (!xamppFound)
            warnings.Add($"Nie znaleziono XAMPP pod: {_drive.GetXamppRootPath()}. Sprawdź ustawienia.");

        // Krok 4: regeneracja vhosts.conf z aktualnymi pełnymi ścieżkami.
        bool vhostsChanged = false;
        try
        {
            var confPath = _drive.GetVHostsConfPath(config.Settings.VHostsConfRelative);
            vhostsChanged = _vhosts.RegenerateFile(confPath, config.Projects, config.Settings);
        }
        catch (Exception ex)
        {
            warnings.Add($"Nie udało się zregenerować httpd-vhosts.conf: {ex.Message}");
        }

        // Krok 5: reconcile lokalnego pliku hosts.
        bool hostsChanged = false;
        var added = new List<string>();
        var removed = new List<string>();
        try
        {
            var hostsPath = HostsFileService.ResolveHostsPath(config.Settings.HostsPathOverride);
            var r = _hosts.Reconcile(hostsPath, config.Projects);
            hostsChanged = r.Changed;
            added = r.Added;
            removed = r.Removed;
        }
        catch (UnauthorizedAccessException)
        {
            warnings.Add("Brak dostępu do pliku hosts. Uruchom program jako administrator. " +
                         "Część programów antywirusowych blokuje zapis do hosts — dodaj wyjątek.");
        }
        catch (Exception ex)
        {
            warnings.Add($"Nie udało się zsynchronizować pliku hosts: {ex.Message}");
        }

        return new SyncResult
        {
            DriveLetter = _drive.DriveLetter,
            MachineName = Environment.MachineName,
            SyncedProjects = CountActive(config),
            VHostsChanged = vhostsChanged,
            HostsChanged = hostsChanged,
            HostsAdded = added,
            HostsRemoved = removed,
            XamppFound = xamppFound,
            Warnings = warnings
        };
    }

    private static int CountActive(AppConfig config)
    {
        int n = 0;
        foreach (var p in config.Projects)
            if (p.Enabled) n++;
        return n;
    }
}
