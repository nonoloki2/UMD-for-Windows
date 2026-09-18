using UMD.Core.Models;

namespace UMD.Core.Engine;

/// <summary>
/// Monta a string de selecao de formato do yt-dlp.
///
/// Porte fiel do _get_format_selector() de ultimate_downloader.py (linha 4117).
/// A logica de fallback encadeado com "/" e a mesma do original: o yt-dlp tenta
/// cada alternativa da esquerda para a direita ate uma casar.
/// </summary>
public static class FormatSelector
{
    /// <summary>
    /// Cadeia de audio do original: FLAC > Opus > M4A > AAC > 320k > 256k > 192k.
    /// </summary>
    private const string AudioChain =
        "bestaudio[acodec=flac]/bestaudio[acodec=opus]/bestaudio[acodec=m4a]/" +
        "bestaudio[acodec=aac]/bestaudio[abr>=320]/bestaudio[abr>=256]/" +
        "bestaudio[abr>=192]/bestaudio/best";

    public static string Build(DownloadRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomFormat))
            return request.CustomFormat!;

        return request.AudioOnly ? AudioChain : BuildVideo(request.Quality);
    }

    private static string BuildVideo(VideoQuality quality) => quality switch
    {
        VideoQuality.Worst => "worst",
        VideoQuality.Best => Chain(2160, fallbackToAnyHeight: true),
        VideoQuality.Q2160p => Chain(2160),
        VideoQuality.Q1440p => Chain(1440),
        VideoQuality.Q1080p => Chain(1080),
        VideoQuality.Q720p => Chain(720),
        VideoQuality.Q480p => Chain(480),
        VideoQuality.Q360p => Chain(360),
        _ => "bestvideo+bestaudio[acodec=flac]/bestvideo+bestaudio[acodec=opus]/bestvideo+bestaudio/best"
    };

    /// <summary>
    /// Para "best" o original termina com "bestvideo+bestaudio/best" sem teto de
    /// altura, o que permite passar de 4K. Para as alturas fixas ele termina com
    /// "best[height&lt;=N]".
    /// </summary>
    private static string Chain(int maxHeight, bool fallbackToAnyHeight = false)
    {
        var tail = fallbackToAnyHeight
            ? "bestvideo+bestaudio/best"
            : $"bestvideo[height<={maxHeight}]+bestaudio/best[height<={maxHeight}]";

        return $"bestvideo[height<={maxHeight}]+bestaudio[acodec=flac]/" +
               $"bestvideo[height<={maxHeight}]+bestaudio[acodec=opus]/" +
               tail;
    }

    /// <summary>
    /// Traduz o nivel de qualidade de audio para o valor VBR do yt-dlp
    /// (--audio-quality). 0 e o melhor, 10 o pior.
    /// </summary>
    public static string ToYtDlpAudioQuality(AudioQuality quality) => quality switch
    {
        AudioQuality.Best => "0",
        AudioQuality.High => "2",
        AudioQuality.Medium => "5",
        AudioQuality.Low => "7",
        _ => "0"
    };

    public static string ToExtension(AudioFormat format) => format switch
    {
        AudioFormat.Mp3 => "mp3",
        AudioFormat.Flac => "flac",
        AudioFormat.Wav => "wav",
        AudioFormat.M4a => "m4a",
        AudioFormat.Opus => "opus",
        AudioFormat.Aac => "aac",
        _ => "mp3"
    };

    public static string? ToExtension(VideoContainer container) => container switch
    {
        VideoContainer.Mp4 => "mp4",
        VideoContainer.Mkv => "mkv",
        VideoContainer.Webm => "webm",
        _ => null
    };
}
