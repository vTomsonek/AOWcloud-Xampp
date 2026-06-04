using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace XamppVHostManager.Services;

/// <summary>Wynik uruchomienia procesu httpd.exe.</summary>
public sealed class ProcessResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;

    /// <summary>Połączony output (stdout + stderr) do pokazania użytkownikowi.</summary>
    public string CombinedOutput =>
        string.Join(Environment.NewLine,
            new[] { Output, Error }).Trim();
}

/// <summary>
/// Obsługuje httpd.exe: test konfiguracji (-t) oraz restart (-k restart).
/// Ścieżka do httpd.exe pochodzi z DriveService (aktualna litera dysku).
/// </summary>
public sealed class ApacheService
{
    /// <summary>
    /// Test konfiguracji: httpd.exe -t. NIE restartuje serwera.
    /// Apache wypisuje wynik na stderr (także "Syntax OK").
    /// </summary>
    public Task<ProcessResult> TestConfigAsync(string httpdExePath) =>
        RunAsync(httpdExePath, "-t");

    /// <summary>Restart Apache: httpd.exe -k restart.</summary>
    public Task<ProcessResult> RestartAsync(string httpdExePath) =>
        RunAsync(httpdExePath, "-k restart");

    private static async Task<ProcessResult> RunAsync(string httpdExePath, string arguments)
    {
        if (string.IsNullOrWhiteSpace(httpdExePath) || !File.Exists(httpdExePath))
        {
            return new ProcessResult
            {
                Success = false,
                ExitCode = -1,
                Error = $"Nie znaleziono httpd.exe pod ścieżką:\n{httpdExePath}\nSprawdź ustawienia / lokalizację XAMPP na dysku."
            };
        }

        var psi = new ProcessStartInfo
        {
            FileName = httpdExePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            // WorkingDirectory = bin\, by httpd znalazł swoje relatywne ścieżki (ServerRoot).
            WorkingDirectory = Path.GetDirectoryName(httpdExePath) ?? Environment.CurrentDirectory
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();

            return new ProcessResult
            {
                Success = proc.ExitCode == 0,
                ExitCode = proc.ExitCode,
                Output = stdout.ToString().Trim(),
                Error = stderr.ToString().Trim()
            };
        }
        catch (Exception ex)
        {
            return new ProcessResult
            {
                Success = false,
                ExitCode = -1,
                Error = $"Błąd uruchomienia httpd.exe: {ex.Message}"
            };
        }
    }
}
