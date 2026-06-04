using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using XamppVHostManager.Models;

namespace XamppVHostManager.Services;

/// <summary>
/// Wynik operacji reconcile na pliku hosts — do pokazania w banerze/logu.
/// </summary>
public sealed class HostsReconcileResult
{
    public bool Changed { get; init; }
    public List<string> Added { get; init; } = new();
    public List<string> Removed { get; init; } = new();
    public int ActiveCount { get; init; }
}

/// <summary>
/// Zarządza plikiem hosts, który jest LOKALNY na każdym komputerze
/// (C:\Windows\System32\drivers\etc\hosts), nie na dysku przenośnym.
///
/// RECONCILE: przy każdym uruchomieniu zapewnia, że w sekcji markerów
/// znajdują się dokładnie ServerName wszystkich AKTYWNYCH projektów z dysku —
/// dodaje brakujące, usuwa nieistniejące. Wpisy użytkownika poza markerami
/// pozostają nietknięte. Dzięki temu vhost dodany na komputerze X pojawia się
/// automatycznie w hosts komputera Y po wpięciu dysku.
/// </summary>
public sealed class HostsFileService
{
    private readonly BackupService _backup;

    public HostsFileService(BackupService backup) => _backup = backup;

    /// <summary>Domyślna systemowa ścieżka pliku hosts (z %SystemRoot%).</summary>
    public static string DefaultHostsPath()
    {
        var sysRoot = Environment.GetEnvironmentVariable("SystemRoot")
                      ?? Environment.GetEnvironmentVariable("windir")
                      ?? @"C:\Windows";
        return Path.Combine(sysRoot, "System32", "drivers", "etc", "hosts");
    }

    /// <summary>Zwraca ścieżkę z ustawień lub domyślną systemową.</summary>
    public static string ResolveHostsPath(string? overridePath) =>
        string.IsNullOrWhiteSpace(overridePath) ? DefaultHostsPath() : overridePath!.Trim();

    /// <summary>
    /// Buduje pojedynczy wpis hosts dla projektu: "127.0.0.1  projekt1.test".
    /// </summary>
    public static string BuildHostsLine(ProjectModel p) => $"127.0.0.1\t{p.ServerName}";

    /// <summary>
    /// Wykonuje reconcile sekcji markerów w pliku hosts wg listy AKTYWNYCH projektów.
    /// Zwraca raport zmian. Robi .bak tylko, gdy faktycznie zapisuje.
    /// </summary>
    public HostsReconcileResult Reconcile(string hostsPath, IEnumerable<ProjectModel> projects)
    {
        var active = projects.Where(p => p.Enabled && ProjectModel.IsServerNameValid(p.ServerName)).ToList();

        // Docelowy zbiór nazw hostów (case-insensitive).
        var desiredNames = active
            .Select(p => p.ServerName.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        EnsureWritable(hostsPath);

        var original = File.Exists(hostsPath) ? File.ReadAllText(hostsPath) : string.Empty;

        // Co aktualnie jest w naszej sekcji?
        var existingNames = ExtractManagedServerNames(original);

        var added = desiredNames
            .Where(n => !existingNames.Contains(n, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var removed = existingNames
            .Where(n => !desiredNames.Contains(n, StringComparer.OrdinalIgnoreCase))
            .ToList();

        // Złóż nową treść sekcji.
        var inner = string.Join("\r\n", desiredNames.Select(n => $"127.0.0.1\t{n}"));
        var updated = MarkerSection.ReplaceSection(original, inner);

        if (original == updated)
        {
            return new HostsReconcileResult
            {
                Changed = false,
                ActiveCount = desiredNames.Count
            };
        }

        _backup.Backup(hostsPath);
        WriteHostsFile(hostsPath, updated);

        return new HostsReconcileResult
        {
            Changed = true,
            Added = added,
            Removed = removed,
            ActiveCount = desiredNames.Count
        };
    }

    /// <summary>
    /// Wyciąga nazwy hostów zarządzane przez nas (z linii w sekcji markerów).
    /// Pomija komentarze i puste linie; bierze drugą kolumnę (po IP).
    /// </summary>
    public List<string> ExtractManagedServerNames(string? content)
    {
        var names = new List<string>();
        foreach (var line in MarkerSection.ExtractInnerLines(content))
        {
            if (line.StartsWith("#"))
                continue;
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            // Format: <IP> <hostname> [hostname2...]
            for (int i = 1; i < parts.Length; i++)
            {
                if (!parts[i].StartsWith("#"))
                    names.Add(parts[i]);
            }
        }
        return names;
    }

    /// <summary>
    /// Sprawdza i — jeśli trzeba — zdejmuje atrybut ReadOnly z pliku hosts.
    /// Niektóre konfiguracje/AV ustawiają hosts jako read-only.
    /// </summary>
    public void EnsureWritable(string hostsPath)
    {
        if (!File.Exists(hostsPath))
            return;

        var attrs = File.GetAttributes(hostsPath);
        if (attrs.HasFlag(FileAttributes.ReadOnly))
            File.SetAttributes(hostsPath, attrs & ~FileAttributes.ReadOnly);
    }

    /// <summary>Czy plik hosts ma atrybut read-only?</summary>
    public bool IsReadOnly(string hostsPath) =>
        File.Exists(hostsPath) && File.GetAttributes(hostsPath).HasFlag(FileAttributes.ReadOnly);

    /// <summary>
    /// Zapis pliku hosts. Plik hosts musi być ASCII/UTF-8 bez BOM — z BOM
    /// niektóre wersje Windows ignorują pierwszą linię.
    /// </summary>
    private static void WriteHostsFile(string hostsPath, string content)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(hostsPath, content, encoding);
    }
}
