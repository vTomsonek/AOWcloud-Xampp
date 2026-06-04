using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using XamppVHostManager.Models;

namespace XamppVHostManager.Services;

/// <summary>
/// Generuje i zapisuje sekcję vhostów w httpd-vhosts.conf na DYSKU.
/// KLUCZOWE: plik jest REGENEROWANY przy każdym starcie z AKTUALNĄ literą
/// dysku — Apache potrzebuje ścieżek absolutnych, a litera zmienia się
/// między komputerami. DocumentRoot zapisujemy w formacie Apache (forward-slash).
///
/// Dla każdego aktywnego projektu generujemy blok HTTP (*:Port). Jeśli w
/// ustawieniach włączone jest HTTPS, dokładamy też blok SSL (*:443) z domyślnym
/// certyfikatem XAMPP — inaczej przeglądarka wchodząca po https trafia na domyślny
/// host SSL XAMPP (serwuje htdocs) zamiast na właściwy DocumentRoot.
/// </summary>
public sealed class VHostFileService
{
    private readonly DriveService _drive;
    private readonly BackupService _backup;

    public VHostFileService(DriveService drive, BackupService backup)
    {
        _drive = drive;
        _backup = backup;
    }

    /// <summary>
    /// Buduje pojedynczy blok &lt;VirtualHost&gt; dla projektu z pełną, aktualną ścieżką.
    /// Gdy <paramref name="ssl"/> = true, dokłada dyrektywy SSL (HTTPS na porcie z ustawień).
    /// </summary>
    public string BuildVHostBlock(ProjectModel p, bool ssl, AppSettings settings)
    {
        // Pełna ścieżka z aktualną literą dysku, w formacie Apache (E:/projekty/sklep).
        // Usuwamy ewentualny końcowy ukośnik — Apache nie lubi go w DocumentRoot.
        var docRoot = _drive.GetApacheDocumentRoot(p.DocumentRootRelative).TrimEnd('/');
        var port = ssl ? settings.HttpsPort : p.Port;
        var logSuffix = ssl ? "-ssl" : "";
        var nl = "\r\n";

        var sb = new StringBuilder();
        sb.Append($"<VirtualHost *:{port}>").Append(nl);
        sb.Append($"    DocumentRoot \"{docRoot}\"").Append(nl);
        sb.Append($"    ServerName {p.ServerName}").Append(nl);
        if (ssl)
        {
            // Certyfikat self-signed z XAMPP-a (ścieżki względne od ServerRoot Apache).
            sb.Append("    SSLEngine on").Append(nl);
            sb.Append($"    SSLCertificateFile \"{settings.EffectiveSslCertFile}\"").Append(nl);
            sb.Append($"    SSLCertificateKeyFile \"{settings.EffectiveSslCertKeyFile}\"").Append(nl);
        }
        sb.Append($"    <Directory \"{docRoot}\">").Append(nl);
        sb.Append("        Require all granted").Append(nl);
        sb.Append("        AllowOverride All").Append(nl);
        sb.Append("        Options Indexes FollowSymLinks").Append(nl);
        sb.Append("    </Directory>").Append(nl);
        sb.Append($"    ErrorLog \"logs/{p.ServerName}{logSuffix}-error.log\"").Append(nl);
        sb.Append("</VirtualHost>");
        return sb.ToString();
    }

    /// <summary>
    /// Składa zawartość WEWNĘTRZNĄ sekcji markerów — bloki wszystkich AKTYWNYCH
    /// projektów (HTTP, a przy włączonym HTTPS także SSL). Nieaktywne pomijane.
    /// </summary>
    public string BuildInnerSection(IEnumerable<ProjectModel> projects, AppSettings settings)
    {
        var active = projects
            .Where(p => p.Enabled)
            .OrderBy(p => p.ServerName, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        var blocks = new List<string>();
        foreach (var p in active)
        {
            blocks.Add(BuildVHostBlock(p, ssl: false, settings));
            if (settings.GenerateHttps)
                blocks.Add(BuildVHostBlock(p, ssl: true, settings));
        }

        return string.Join("\r\n\r\n", blocks);
    }

    /// <summary>
    /// Regeneruje sekcję vhostów w pliku conf. Robi .bak przed zapisem,
    /// nie tyka treści poza markerami. Zwraca true, jeśli zawartość się zmieniła.
    /// </summary>
    public bool RegenerateFile(string vhostsConfPath, IEnumerable<ProjectModel> projects, AppSettings settings)
    {
        var inner = BuildInnerSection(projects, settings);

        var original = File.Exists(vhostsConfPath) ? File.ReadAllText(vhostsConfPath) : string.Empty;
        var updated = MarkerSection.ReplaceSection(original, inner);

        if (original == updated)
            return false; // nic się nie zmieniło — nie ruszamy pliku

        // Upewnij się, że katalog istnieje (np. extra\), zrób backup, zapisz.
        var dir = Path.GetDirectoryName(vhostsConfPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _backup.Backup(vhostsConfPath);
        File.WriteAllText(vhostsConfPath, updated, new UTF8Encoding(false));
        return true;
    }
}
