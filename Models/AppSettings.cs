namespace XamppVHostManager.Models;

/// <summary>
/// Ustawienia aplikacji. Ścieżki do plików na DYSKU przenośnym trzymamy
/// jako WZGLĘDNE od litery dysku (np. "xampp\apache\conf\extra\httpd-vhosts.conf").
/// Ścieżka do pliku hosts jest LOKALNA na hoście — domyślnie stała systemowa,
/// ale można nadpisać.
/// Puste pole => użyj wartości domyślnej (auto-wykrytej w runtime).
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Ścieżka WZGLĘDNA (od litery dysku) do httpd-vhosts.conf.
    /// Pusta => domyślnie "xampp\apache\conf\extra\httpd-vhosts.conf".
    /// </summary>
    public string VHostsConfRelative { get; set; } = "";

    /// <summary>
    /// Ścieżka WZGLĘDNA (od litery dysku) do httpd.exe.
    /// Pusta => domyślnie "xampp\apache\bin\httpd.exe".
    /// </summary>
    public string HttpdExeRelative { get; set; } = "";

    /// <summary>
    /// Ścieżka do pliku hosts. LOKALNA na komputerze (nie na dysku).
    /// Pusta => domyślnie C:\Windows\System32\drivers\etc\hosts (z %SystemRoot%).
    /// </summary>
    public string HostsPathOverride { get; set; } = "";

    /// <summary>Czy pytać przed restartem Apache po zmianach.</summary>
    public bool AskBeforeRestart { get; set; } = true;

    /// <summary>
    /// Czy dla każdego projektu generować TAKŻE blok HTTPS (&lt;VirtualHost *:443&gt;).
    /// Domyślnie tak — bo przeglądarki (zwłaszcza dla domen .ai) wymuszają HTTPS,
    /// a bez tego bloku 443 obsługuje domyślny host SSL XAMPP (serwuje htdocs).
    /// </summary>
    public bool GenerateHttps { get; set; } = true;

    /// <summary>Port HTTPS dla generowanych bloków SSL. Domyślnie 443.</summary>
    public int HttpsPort { get; set; } = 443;

    /// <summary>
    /// Ścieżka do certyfikatu SSL — WZGLĘDNA od ServerRoot Apache (katalog xampp\apache).
    /// Pusta => domyślny certyfikat XAMPP "conf/ssl.crt/server.crt".
    /// </summary>
    public string SslCertFile { get; set; } = "";

    /// <summary>
    /// Ścieżka do klucza SSL — WZGLĘDNA od ServerRoot Apache.
    /// Pusta => domyślny klucz XAMPP "conf/ssl.key/server.key".
    /// </summary>
    public string SslCertKeyFile { get; set; } = "";

    // ---- Wartości domyślne (względne / systemowe) ----
    public const string DefaultVHostsConfRelative = @"xampp\apache\conf\extra\httpd-vhosts.conf";
    public const string DefaultHttpdExeRelative = @"xampp\apache\bin\httpd.exe";
    public const string DefaultXamppRootRelative = "xampp";
    public const string DefaultSslCertFile = "conf/ssl.crt/server.crt";
    public const string DefaultSslCertKeyFile = "conf/ssl.key/server.key";

    /// <summary>Efektywna ścieżka certyfikatu (ustawienie lub domyślna XAMPP).</summary>
    public string EffectiveSslCertFile =>
        string.IsNullOrWhiteSpace(SslCertFile) ? DefaultSslCertFile : SslCertFile.Replace('\\', '/');

    /// <summary>Efektywna ścieżka klucza (ustawienie lub domyślna XAMPP).</summary>
    public string EffectiveSslCertKeyFile =>
        string.IsNullOrWhiteSpace(SslCertKeyFile) ? DefaultSslCertKeyFile : SslCertKeyFile.Replace('\\', '/');

    public AppSettings Clone() => new()
    {
        VHostsConfRelative = VHostsConfRelative,
        HttpdExeRelative = HttpdExeRelative,
        HostsPathOverride = HostsPathOverride,
        AskBeforeRestart = AskBeforeRestart,
        GenerateHttps = GenerateHttps,
        HttpsPort = HttpsPort,
        SslCertFile = SslCertFile,
        SslCertKeyFile = SslCertKeyFile
    };
}
