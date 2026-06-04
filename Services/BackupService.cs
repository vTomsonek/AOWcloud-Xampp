using System;
using System.IO;

namespace XamppVHostManager.Services;

/// <summary>
/// Tworzy kopie .bak (z timestampem) edytowanych plików do
/// &lt;dysk&gt;\XamppVHostManager\backups\ PRZED każdym zapisem.
/// </summary>
public sealed class BackupService
{
    private readonly DriveService _drive;

    public BackupService(DriveService drive) => _drive = drive;

    /// <summary>
    /// Kopiuje istniejący plik do katalogu backupów z nazwą
    /// &lt;nazwa&gt;.&lt;yyyyMMdd-HHmmss&gt;.bak. Jeśli plik nie istnieje — nic nie robi
    /// i zwraca null (nie ma czego archiwizować).
    /// </summary>
    public string? Backup(string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
            return null;

        _drive.EnsureDataDirectories();

        var name = Path.GetFileName(sourceFilePath);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var target = Path.Combine(_drive.BackupsDirectory, $"{name}.{stamp}.bak");

        // Gdy w tej samej sekundzie powstaje kolejny backup — dołóż licznik.
        var counter = 1;
        while (File.Exists(target))
        {
            target = Path.Combine(_drive.BackupsDirectory, $"{name}.{stamp}-{counter}.bak");
            counter++;
        }

        File.Copy(sourceFilePath, target, overwrite: false);
        return target;
    }
}
