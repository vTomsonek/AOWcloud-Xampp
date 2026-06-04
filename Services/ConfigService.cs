using System;
using System.IO;
using System.Text.Json;
using XamppVHostManager.Models;

namespace XamppVHostManager.Services;

/// <summary>
/// Wczytuje i zapisuje config.json z/do &lt;dysk&gt;\XamppVHostManager\.
/// To JEDYNE źródło prawdy o liście vhostów — leży na dysku przenośnym.
/// Ścieżki projektów są w pliku WZGLĘDNE (przenośność).
/// </summary>
public sealed class ConfigService
{
    private readonly DriveService _drive;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Czytamy też niezależnie od wielkości liter, gdyby ktoś ręcznie edytował.
        PropertyNameCaseInsensitive = true
    };

    public ConfigService(DriveService drive) => _drive = drive;

    /// <summary>
    /// Wczytuje config z dysku. Gdy pliku nie ma — zwraca świeży, pusty config
    /// (pierwsze uruchomienie). Uszkodzony JSON => wyjątek z czytelnym komunikatem.
    /// </summary>
    public AppConfig Load()
    {
        _drive.EnsureDataDirectories();

        if (!File.Exists(_drive.ConfigFilePath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(_drive.ConfigFilePath);
            if (string.IsNullOrWhiteSpace(json))
                return new AppConfig();

            var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
            return cfg ?? new AppConfig();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Plik config.json jest uszkodzony i nie da się go odczytać:\n{_drive.ConfigFilePath}\n\nSzczegóły: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Zapisuje config na dysk. Zapis atomowy: najpierw plik tymczasowy,
    /// potem podmiana — żeby nie zostawić uszkodzonego config.json przy przerwaniu.
    /// </summary>
    public void Save(AppConfig config)
    {
        _drive.EnsureDataDirectories();

        var json = JsonSerializer.Serialize(config, JsonOpts);
        var tmp = _drive.ConfigFilePath + ".tmp";
        File.WriteAllText(tmp, json);

        if (File.Exists(_drive.ConfigFilePath))
            File.Replace(tmp, _drive.ConfigFilePath, null);
        else
            File.Move(tmp, _drive.ConfigFilePath);
    }
}
