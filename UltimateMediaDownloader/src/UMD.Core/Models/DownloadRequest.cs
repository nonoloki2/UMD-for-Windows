namespace UMD.Core.Models;

/// <summary>
/// Tudo que o usuario escolheu para um download. Substitui os 13 parametros
/// soltos do download_media() do Python, que era impossivel de chamar sem errar
/// a ordem.
/// </summary>
public sealed record DownloadRequest
{
    public required string Url { get; init; }

    public required string OutputDirectory { get; init; }

    public bool AudioOnly { get; init; }

    public VideoQuality Quality { get; init; } = VideoQuality.Best;

    public VideoContainer Container { get; init; } = VideoContainer.Best;

    public AudioFormat AudioFormat { get; init; } = AudioFormat.Mp3;

    public AudioQuality AudioQuality { get; init; } = AudioQuality.Best;

    /// <summary>Seletor de formato cru do yt-dlp. Se preenchido, ignora Quality/AudioOnly.</summary>
    public string? CustomFormat { get; init; }

    public bool EmbedMetadata { get; init; } = true;

    public bool EmbedThumbnail { get; init; } = true;

    public bool DownloadSubtitles { get; init; }

    public string SubtitleLanguage { get; init; } = "en";

    /// <summary>Codigo de idioma da faixa de audio, para videos multi-idioma.</summary>
    public string? AudioLanguage { get; init; }

    /// <summary>
    /// null  = comportamento padrao do yt-dlp
    /// true  = baixa a playlist inteira (--yes-playlist)
    /// false = baixa so o item apontado (--no-playlist)
    /// </summary>
    public bool? DownloadPlaylist { get; init; }

    /// <summary>Indice inicial da playlist, base 1. Mapeia para --playlist-items.</summary>
    public int PlaylistStartIndex { get; init; } = 1;

    public int? MaxDownloads { get; init; }

    /// <summary>Template de nome de arquivo do yt-dlp (-o).</summary>
    public string OutputTemplate { get; init; } = "%(title).200B.%(ext)s";

    /// <summary>Limite de banda, ex.: "2M". Mapeia para --limit-rate.</summary>
    public string? RateLimit { get; init; }

    public int Retries { get; init; } = 10;

    /// <summary>Caminho de um cookies.txt no formato Netscape.</summary>
    public string? CookieFile { get; init; }

    /// <summary>Navegador de onde extrair cookies, ex.: "chrome", "firefox", "edge".</summary>
    public string? CookiesFromBrowser { get; init; }

    public string? ProxyUrl { get; init; }
}

/// <summary>
/// Um instantaneo de progresso emitido pelo yt-dlp durante o download.
/// </summary>
public sealed record DownloadProgress
{
    public long DownloadedBytes { get; init; }

    /// <summary>Total real ou estimado. Nulo quando o servidor nao informa o tamanho.</summary>
    public long? TotalBytes { get; init; }

    /// <summary>Velocidade em bytes por segundo.</summary>
    public double? SpeedBytesPerSecond { get; init; }

    public double? EtaSeconds { get; init; }

    /// <summary>Status cru do yt-dlp: "downloading", "finished", "error".</summary>
    public string Status { get; init; } = "downloading";

    /// <summary>Titulo do item atual. Util em playlists.</summary>
    public string? Title { get; init; }

    /// <summary>Indice do item na playlist, base 1. Nulo em midia unica.</summary>
    public int? PlaylistIndex { get; init; }

    public int? PlaylistCount { get; init; }

    /// <summary>
    /// Identificador do formato sendo baixado agora, ex.: "137" (video) ou
    /// "140" (audio).
    /// </summary>
    public string? FormatId { get; init; }

    /// <summary>
    /// Qual faixa deste item esta sendo baixada, base 1.
    ///
    /// Video em alta qualidade vem em DASH: o yt-dlp baixa a faixa de video,
    /// depois a de audio, e o ffmpeg junta. Cada faixa tem progresso proprio
    /// comecando do zero, entao sem este contador a barra encheria, voltaria a
    /// zero e encheria de novo.
    /// </summary>
    public int PartIndex { get; init; } = 1;

    /// <summary>Bytes ja concluidos das faixas anteriores deste mesmo item.</summary>
    public long PreviousPartsBytes { get; init; }

    /// <summary>Total acumulado do item, somando as faixas ja concluidas.</summary>
    public long TotalDownloadedBytes => PreviousPartsBytes + DownloadedBytes;

    /// <summary>Percentual de 0 a 100. Nulo quando o tamanho total e desconhecido.</summary>
    public double? Percent =>
        TotalBytes is > 0 ? Math.Clamp(DownloadedBytes * 100.0 / TotalBytes.Value, 0, 100) : null;

    public string FormatSpeed() =>
        SpeedBytesPerSecond is { } s and > 0 ? $"{FormatBytes((long)s)}/s" : "--";

    public string FormatEta()
    {
        if (EtaSeconds is not { } e || e <= 0) return "--:--";
        var t = TimeSpan.FromSeconds(e);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }
}

/// <summary>
/// Resultado final de um download.
/// </summary>
public sealed class DownloadResult
{
    public required bool Success { get; init; }

    /// <summary>Caminhos dos arquivos gerados, ja apos pos-processamento.</summary>
    public List<string> FilePaths { get; init; } = new();

    public string? ErrorMessage { get; init; }

    /// <summary>Saida completa do processo, para a janela de log.</summary>
    public string? RawOutput { get; init; }

    public int ExitCode { get; init; }

    public static DownloadResult Ok(List<string> paths, string? raw = null) =>
        new() { Success = true, FilePaths = paths, RawOutput = raw };

    public static DownloadResult Fail(string message, int exitCode = -1, string? raw = null) =>
        new() { Success = false, ErrorMessage = message, ExitCode = exitCode, RawOutput = raw };
}