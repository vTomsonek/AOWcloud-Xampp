using System.Collections.Generic;

namespace XamppVHostManager.Models;

/// <summary>
/// Główny obiekt konfiguracji zapisywany do
/// &lt;dysk&gt;\XamppVHostManager\config.json.
/// To JEDYNE źródło prawdy o liście vhostów (leży na dysku przenośnym).
/// </summary>
public sealed class AppConfig
{
    /// <summary>Wersja schematu pliku (na przyszłe migracje).</summary>
    public int SchemaVersion { get; set; } = 1;

    public AppSettings Settings { get; set; } = new();

    public List<ProjectModel> Projects { get; set; } = new();
}
