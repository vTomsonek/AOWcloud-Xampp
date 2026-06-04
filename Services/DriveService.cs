using System;
using System.IO;
using System.Reflection;

namespace XamppVHostManager.Services;

/// <summary>
/// FUNDAMENT PRZENOŚNOŚCI.
///
/// Aplikacja, jej dane i XAMPP leżą RAZEM na dysku przenośnym. Na różnych
/// komputerach ten sam dysk dostaje różne litery (S:, E:, F:...). Ta klasa
/// wykrywa aktualną literę dysku w RUNTIME (na podstawie położenia .exe)
/// i tłumaczy ścieżki WZGLĘDNE (zapisane w config.json) na pełne ścieżki
/// z bieżącą literą — i odwrotnie.
///
/// ZASADA: w danych trzymamy TYLKO ścieżki względne. Literę dysku doklejamy
/// dopiero przy starcie/zapisie. Nigdy nie hardkodujemy "S:".
/// </summary>
public sealed class DriveService
{
    /// <summary>Korzeń dysku przenośnego z bieżącą literą, np. "E:\".</summary>
    public string DriveRoot { get; }

    /// <summary>Sama litera z dwukropkiem, np. "E:".</summary>
    public string DriveLetter { get; }

    /// <summary>Katalog, w którym leży .exe (BaseDirectory).</summary>
    public string AppDirectory { get; }

    /// <summary>Katalog danych programu OBOK .exe: &lt;katalog .exe&gt;\AOWcloud-Xampp.</summary>
    public string DataDirectory { get; }

    /// <summary>Katalog kopii zapasowych: &lt;katalog .exe&gt;\AOWcloud-Xampp\backups.</summary>
    public string BackupsDirectory { get; }

    /// <summary>Plik konfiguracji: &lt;katalog .exe&gt;\AOWcloud-Xampp\config.json.</summary>
    public string ConfigFilePath { get; }

    public DriveService()
    {
        AppDirectory = ResolveAppDirectory();

        // Korzeń ścieżki = litera dysku, na którym faktycznie leży .exe.
        // Path.GetPathRoot zwraca np. "E:\". To jest wykrycie litery w runtime,
        // bez żadnego hardkodowania.
        var root = Path.GetPathRoot(AppDirectory);
        if (string.IsNullOrEmpty(root))
            throw new InvalidOperationException(
                "Nie udało się wykryć litery dysku z położenia aplikacji: " + AppDirectory);

        DriveRoot = root;                                  // "E:\"
        DriveLetter = root.TrimEnd('\\', '/');             // "E:"

        // Dane programu (config.json, backups) trzymamy OBOK .exe, a nie w korzeniu
        // dysku — dzięki temu cały zestaw (exe + dane) jest w jednym katalogu i można
        // go przenosić jako całość. Ścieżki PROJEKTÓW nadal liczone są od litery
        // dysku (DriveRoot), bo Apache wymaga ścieżek absolutnych.
        DataDirectory = Path.Combine(AppDirectory, "AOWcloud-Xampp");
        BackupsDirectory = Path.Combine(DataDirectory, "backups");
        ConfigFilePath = Path.Combine(DataDirectory, "config.json");
    }

    /// <summary>
    /// Ustala katalog aplikacji. AppContext.BaseDirectory działa również dla
    /// publikacji single-file (wskazuje na katalog .exe, nie na temp z self-extract).
    /// Fallback na lokalizację assembly, gdyby BaseDirectory był pusty.
    /// </summary>
    private static string ResolveAppDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
            return baseDir.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;

        // Fallback — Assembly location (może być pusty w single-file, stąd dopiero tu,
        // PO sprawdzeniu BaseDirectory). Świadomie tłumimy IL3000: w single-file
        // ta ścieżka i tak zwróci "", więc poniższy if jej nie użyje — to tylko
        // zabezpieczenie dla wariantu framework-dependent.
#pragma warning disable IL3000
        var asmPath = Assembly.GetExecutingAssembly().Location;
#pragma warning restore IL3000
        if (!string.IsNullOrWhiteSpace(asmPath))
        {
            var dir = Path.GetDirectoryName(asmPath);
            if (!string.IsNullOrWhiteSpace(dir))
                return dir.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
        }

        // Ostateczny fallback — bieżący katalog procesu.
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>
    /// Rozwija ścieżkę WZGLĘDNĄ (od korzenia dysku) do pełnej ścieżki Windows
    /// z bieżącą literą. Jeśli ktoś poda już ścieżkę absolutną — zwraca ją bez zmian.
    /// Przykład: "projekty\sklep" + dysk E: => "E:\projekty\sklep".
    /// </summary>
    public string ToAbsolute(string relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
            return DriveRoot;

        var p = relativeOrAbsolute.Trim();

        // Już absolutna (ma literę dysku lub UNC)? Zostaw.
        if (Path.IsPathRooted(p) && (p.Length >= 2 && p[1] == ':' || p.StartsWith(@"\\")))
            return p;

        // Usuń ewentualny wiodący separator, by Combine zadziałał poprawnie.
        p = p.TrimStart('\\', '/');
        return Path.GetFullPath(Path.Combine(DriveRoot, p));
    }

    /// <summary>
    /// Zamienia pełną ścieżkę na WZGLĘDNĄ od korzenia bieżącego dysku.
    /// Używane przy zapisie wyboru z folder-pickera do config.json.
    /// Jeśli ścieżka jest na INNYM dysku niż przenośny — zwraca pustą wartość,
    /// bo nie da się jej zapisać przenośnie (wywołujący ma ostrzec użytkownika).
    /// </summary>
    public string ToRelative(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return string.Empty;

        var full = Path.GetFullPath(absolutePath.Trim());

        if (!IsOnPortableDrive(full))
            return string.Empty; // sygnał: poza dyskiem przenośnym

        var rel = Path.GetRelativePath(DriveRoot, full);
        // GetRelativePath nie zaczyna od separatora; normalizujemy do backslashy.
        return rel.Replace('/', '\\').TrimStart('\\');
    }

    /// <summary>Czy podana ścieżka leży na dysku przenośnym (tej samej literze co .exe)?</summary>
    public bool IsOnPortableDrive(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
            return false;
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(absolutePath.Trim()));
            return string.Equals(root, DriveRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Zamienia ścieżkę Windows na format z UKOŚNIKAMI W PRZÓD dla Apache.
    /// Apache w httpd.conf woli forward-slashe nawet na Windows.
    /// Przykład: "E:\projekty\sklep" => "E:/projekty/sklep".
    /// </summary>
    public static string ToApachePath(string windowsPath) =>
        (windowsPath ?? string.Empty).Replace('\\', '/');

    /// <summary>
    /// Pełna ścieżka DocumentRoot projektu w formacie Apache (forward-slash),
    /// gotowa do wstawienia do bloku &lt;VirtualHost&gt;.
    /// </summary>
    public string GetApacheDocumentRoot(string documentRootRelative) =>
        ToApachePath(ToAbsolute(documentRootRelative));

    /// <summary>Pełna ścieżka do httpd-vhosts.conf wg ustawień (lub domyślna).</summary>
    public string GetVHostsConfPath(string? relativeFromSettings)
    {
        var rel = string.IsNullOrWhiteSpace(relativeFromSettings)
            ? Models.AppSettings.DefaultVHostsConfRelative
            : relativeFromSettings!;
        return ToAbsolute(rel);
    }

    /// <summary>Pełna ścieżka do httpd.exe wg ustawień (lub domyślna).</summary>
    public string GetHttpdExePath(string? relativeFromSettings)
    {
        var rel = string.IsNullOrWhiteSpace(relativeFromSettings)
            ? Models.AppSettings.DefaultHttpdExeRelative
            : relativeFromSettings!;
        return ToAbsolute(rel);
    }

    /// <summary>Pełna ścieżka do katalogu głównego XAMPP na dysku.</summary>
    public string GetXamppRootPath() => ToAbsolute(Models.AppSettings.DefaultXamppRootRelative);

    /// <summary>Czy XAMPP istnieje pod domyślną lokalizacją na dysku?</summary>
    public bool XamppExists() => Directory.Exists(GetXamppRootPath());

    /// <summary>Tworzy katalogi danych programu, jeśli nie istnieją.</summary>
    public void EnsureDataDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
    }
}
