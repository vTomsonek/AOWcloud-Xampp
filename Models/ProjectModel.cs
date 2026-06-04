using System.Text.RegularExpressions;

namespace XamppVHostManager.Models;

/// <summary>
/// Model pojedynczego projektu (wirtualnego hosta).
/// UWAGA dot. przenośności: <see cref="DocumentRootRelative"/> jest ZAWSZE
/// zapisywany WZGLĘDNIE do katalogu głównego dysku przenośnego
/// (np. "projekty\sklep"). Pełna ścieżka z aktualną literą dysku jest
/// wyliczana dopiero w runtime przez DriveService.
/// </summary>
public sealed class ProjectModel
{
    /// <summary>Nazwa hosta, np. "projekt1.test". Sugerowana końcówka: .test</summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>Ścieżka WZGLĘDNA od litery dysku, np. "projekty\sklep". NIGDY absolutna.</summary>
    public string DocumentRootRelative { get; set; } = string.Empty;

    /// <summary>Port nasłuchu vhosta. Domyślnie 80.</summary>
    public int Port { get; set; } = 80;

    /// <summary>Czy vhost jest aktywny (generowany i wpinany do hosts).</summary>
    public bool Enabled { get; set; } = true;

    // ------- Walidacja -------

    // Dozwolone znaki w nazwie hosta: litery, cyfry, kropka i myślnik (etykiety DNS).
    private static readonly Regex ServerNameRegex =
        new(@"^(?=.{1,253}$)([a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)(\.[a-zA-Z0-9](?:[a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?)+$",
            RegexOptions.Compiled);

    /// <summary>Czy ServerName jest poprawną nazwą hosta (wieloetykietową).</summary>
    public bool IsServerNameValid() => IsServerNameValid(ServerName);

    public static bool IsServerNameValid(string? name) =>
        !string.IsNullOrWhiteSpace(name) && ServerNameRegex.IsMatch(name.Trim());

    /// <summary>Sugeruje nazwę z końcówką .test, jeśli użytkownik nie podał kropki.</summary>
    public static string SuggestServerName(string? raw)
    {
        var s = (raw ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(s)) return string.Empty;
        if (!s.Contains('.')) s += ".test";
        return s;
    }

    public ProjectModel Clone() => new()
    {
        ServerName = ServerName,
        DocumentRootRelative = DocumentRootRelative,
        Port = Port,
        Enabled = Enabled
    };
}
