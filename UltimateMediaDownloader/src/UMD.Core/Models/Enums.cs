namespace UMD.Core.Models;

/// <summary>
/// Qualidade de video pedida pelo usuario. Equivale ao --quality do UMD original.
/// </summary>
public enum VideoQuality
{
    Best,
    Q2160p,
    Q1440p,
    Q1080p,
    Q720p,
    Q480p,
    Q360p,
    Worst
}

/// <summary>
/// Formato de saida de audio quando o download e somente audio.
/// </summary>
public enum AudioFormat
{
    Mp3,
    Flac,
    Wav,
    M4a,
    Opus,
    Aac
}

/// <summary>
/// Container de video de saida. Best = nao remuxa, mantem o que veio.
/// </summary>
public enum VideoContainer
{
    Best,
    Mp4,
    Mkv,
    Webm
}

/// <summary>
/// Nivel de qualidade de audio. Mapeia para o --audio-quality do yt-dlp
/// (0 = melhor VBR, 10 = pior).
/// </summary>
public enum AudioQuality
{
    Best,
    High,
    Medium,
    Low
}

/// <summary>
/// Estado de um item na fila de download.
/// </summary>
public enum JobStatus
{
    Pending,
    FetchingInfo,
    Downloading,
    PostProcessing,
    Completed,
    Failed,
    Cancelled
}
