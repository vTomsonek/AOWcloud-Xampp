using System;
using System.Collections.Generic;
using System.Text;

namespace XamppVHostManager.Services;

/// <summary>
/// Wspólna logika sekcji między markerami — używana zarówno dla
/// httpd-vhosts.conf, jak i dla pliku hosts.
///
/// Program zarządza WYŁĄCZNIE treścią między markerami:
///     # === AOWcloud Xampp START ===
///     ...nasze wpisy...
///     # === AOWcloud Xampp END ===
/// Wszystko poza markerami to wpisy użytkownika — NIETYKALNE.
/// Brak markerów => sekcję dopisujemy na końcu pliku.
/// </summary>
public static class MarkerSection
{
    public const string StartMarker = "# === AOWcloud Xampp START ===";
    public const string EndMarker = "# === AOWcloud Xampp END ===";

    /// <summary>
    /// Zwraca nową zawartość pliku, w której blok między markerami zostaje
    /// zastąpiony przez <paramref name="newInnerContent"/>. Treść poza markerami
    /// pozostaje bez zmian. Jeśli markerów brak — sekcja jest dopisywana na końcu.
    /// Zachowuje styl końca linii (CRLF/LF) wykryty w oryginale.
    /// </summary>
    public static string ReplaceSection(string? originalContent, string newInnerContent)
    {
        originalContent ??= string.Empty;
        var nl = DetectNewLine(originalContent);

        var startIdx = originalContent.IndexOf(StartMarker, StringComparison.Ordinal);
        var endIdx = originalContent.IndexOf(EndMarker, StringComparison.Ordinal);

        var inner = (newInnerContent ?? string.Empty).TrimEnd('\r', '\n');

        // Złóż kompletny blok sekcji (markery + treść).
        var section = new StringBuilder();
        section.Append(StartMarker).Append(nl);
        if (inner.Length > 0)
            section.Append(NormalizeNewLines(inner, nl)).Append(nl);
        section.Append(EndMarker);

        // Przypadek 1: oba markery istnieją i są w poprawnej kolejności -> podmień blok.
        if (startIdx >= 0 && endIdx > startIdx)
        {
            var before = originalContent[..startIdx];
            var afterStart = endIdx + EndMarker.Length;
            var after = originalContent[afterStart..];
            return before + section + after;
        }

        // Przypadek 2: brak (lub uszkodzone) markery -> dopisz sekcję na końcu pliku.
        var sb = new StringBuilder(originalContent.TrimEnd('\r', '\n'));
        if (sb.Length > 0)
            sb.Append(nl).Append(nl); // pusta linia odstępu od treści użytkownika
        sb.Append(section).Append(nl);
        return sb.ToString();
    }

    /// <summary>
    /// Wyciąga linie znajdujące się WEWNĄTRZ sekcji markerów (bez samych markerów).
    /// Gdy markerów brak — zwraca pustą listę.
    /// </summary>
    public static List<string> ExtractInnerLines(string? content)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(content))
            return result;

        var startIdx = content.IndexOf(StartMarker, StringComparison.Ordinal);
        var endIdx = content.IndexOf(EndMarker, StringComparison.Ordinal);
        if (startIdx < 0 || endIdx <= startIdx)
            return result;

        var innerStart = startIdx + StartMarker.Length;
        var inner = content[innerStart..endIdx];
        foreach (var line in inner.Replace("\r\n", "\n").Split('\n'))
        {
            var t = line.Trim();
            if (t.Length > 0)
                result.Add(t);
        }
        return result;
    }

    /// <summary>Wykrywa dominujący styl końca linii w pliku (domyślnie CRLF na Windows).</summary>
    public static string DetectNewLine(string content)
    {
        if (string.IsNullOrEmpty(content))
            return "\r\n";
        // Jeśli są CRLF, użyj CRLF; jeśli tylko LF — użyj LF.
        var crlf = CountOccurrences(content, "\r\n");
        var lfTotal = CountOccurrences(content, "\n");
        var loneLf = lfTotal - crlf;
        return loneLf > crlf ? "\n" : "\r\n";
    }

    private static string NormalizeNewLines(string text, string nl) =>
        text.Replace("\r\n", "\n").Replace("\n", nl);

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
