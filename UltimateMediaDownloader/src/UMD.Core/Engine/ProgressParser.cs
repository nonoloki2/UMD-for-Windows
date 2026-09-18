using System.Globalization;
using UMD.Core.Models;

namespace UMD.Core.Engine;

/// <summary>
/// Le o progresso do yt-dlp.
///
/// O UMD original usava progress_hook, um callback Python chamado dentro do
/// processo. Como aqui o yt-dlp roda como processo separado, usamos
/// --progress-template para que ele imprima uma linha estruturada que da para
/// parsear com seguranca, em vez de tentar extrair numeros da barra colorida.
/// </summary>
public static class ProgressParser
{
    /// <summary>Prefixo que marca nossas linhas e as separa do resto da saida.</summary>
    public const string Marker = "UMDPROG";

    private const char Separator = '\u0001';

    /// <summary>Prefixo das linhas que informam o caminho final de um arquivo.</summary>
    public const string FileMarker = "UMDFILE";

    /// <summary>
    /// Template do --print que informa o caminho final de cada arquivo, ja apos
    /// conversao e merge. O marcador evita ter que adivinhar se uma linha solta
    /// da saida e um caminho: sem ele, um titulo de video que parecesse um
    /// caminho seria confundido, e um caminho de verdade poderia passar batido.
    /// </summary>
    public static string BuildFileTemplate() =>
        $"after_move:{FileMarker}{Separator}%(filepath)s";

    /// <summary>
    /// Tenta extrair o caminho final de um arquivo de uma linha de saida.
    /// </summary>
    public static string? TryParseFilePath(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;

        var start = line.IndexOf(FileMarker, StringComparison.Ordinal);
        if (start < 0) return null;

        var parts = line[start..].Split(Separator);
        return parts.Length >= 2 ? Text(parts[1]) : null;
    }

    /// <summary>
    /// Template passado ao yt-dlp. Campos em ordem:
    /// status, downloaded_bytes, total_bytes, total_bytes_estimate, speed, eta,
    /// title, playlist_index, playlist_count, format_id.
    ///
    /// Campos indisponiveis viram "NA", que e o texto que o yt-dlp usa para nulo.
    /// </summary>
    public static string BuildTemplate() =>
        $"download:{Marker}{Separator}" +
        $"%(progress.status)s{Separator}" +
        $"%(progress.downloaded_bytes)s{Separator}" +
        $"%(progress.total_bytes)s{Separator}" +
        $"%(progress.total_bytes_estimate)s{Separator}" +
        $"%(progress.speed)s{Separator}" +
        $"%(progress.eta)s{Separator}" +
        $"%(info.title)s{Separator}" +
        $"%(info.playlist_index)s{Separator}" +
        $"%(info.playlist_count)s{Separator}" +
        $"%(info.format_id)s";

    /// <summary>
    /// Tenta converter uma linha de saida em DownloadProgress.
    /// Devolve null para qualquer linha que nao seja nossa.
    /// </summary>
    public static DownloadProgress? TryParse(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;

        var start = line.IndexOf(Marker, StringComparison.Ordinal);
        if (start < 0) return null;

        var parts = line[start..].Split(Separator);
        if (parts.Length < 11) return null;

        return new DownloadProgress
        {
            Status = Text(parts[1]) ?? "downloading",
            DownloadedBytes = Long(parts[2]) ?? 0,
            TotalBytes = Long(parts[3]) ?? Long(parts[4]),
            SpeedBytesPerSecond = Double(parts[5]),
            EtaSeconds = Double(parts[6]),
            Title = Text(parts[7]),
            PlaylistIndex = Int(parts[8]),
            PlaylistCount = Int(parts[9]),
            FormatId = Text(parts[10])
        };
    }

    private static bool IsNull(string v) =>
        string.IsNullOrWhiteSpace(v) || v is "NA" or "None" or "none";

    private static string? Text(string v) => IsNull(v) ? null : v.Trim();

    private static long? Long(string v) =>
        Double(v) is { } d ? (long)d : null;

    private static int? Int(string v) =>
        Double(v) is { } d ? (int)d : null;

    private static double? Double(string v)
    {
        if (IsNull(v)) return null;
        return double.TryParse(v.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }
}