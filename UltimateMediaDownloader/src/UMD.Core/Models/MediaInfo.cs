using System.Text.Json;
using System.Text.Json.Serialization;

namespace UMD.Core.Models;

/// <summary>
/// Metadados de uma midia, preenchidos a partir do JSON que o
/// "yt-dlp --dump-single-json" devolve.
///
/// Substitui o dicionario "info_dict" que o codigo Python passava de metodo
/// em metodo sem nenhuma tipagem. Aqui o compilador garante a estrutura.
/// </summary>
public sealed class MediaInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("uploader")]
    public string? Uploader { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    /// <summary>Duracao em segundos. Nulo em lives e em playlists.</summary>
    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("view_count")]
    public long? ViewCount { get; set; }

    [JsonPropertyName("like_count")]
    public long? LikeCount { get; set; }

    [JsonPropertyName("upload_date")]
    public string? UploadDate { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? ThumbnailUrl { get; set; }

    [JsonPropertyName("webpage_url")]
    public string? WebpageUrl { get; set; }

    [JsonPropertyName("original_url")]
    public string? OriginalUrl { get; set; }

    /// <summary>Nome do extractor do yt-dlp, ex.: "youtube", "vimeo", "soundcloud".</summary>
    [JsonPropertyName("extractor")]
    public string? Extractor { get; set; }

    [JsonPropertyName("extractor_key")]
    public string? ExtractorKey { get; set; }

    /// <summary>"video" ou "playlist". O yt-dlp so preenche em alguns extractors.</summary>
    [JsonPropertyName("_type")]
    public string? Type { get; set; }

    [JsonPropertyName("is_live")]
    public bool? IsLive { get; set; }

    [JsonPropertyName("artist")]
    public string? Artist { get; set; }

    [JsonPropertyName("album")]
    public string? Album { get; set; }

    [JsonPropertyName("track")]
    public string? Track { get; set; }

    [JsonPropertyName("release_year")]
    public int? ReleaseYear { get; set; }

    [JsonPropertyName("formats")]
    public List<MediaFormat> Formats { get; set; } = new();

    /// <summary>
    /// Itens de uma playlist. Vem preenchido quando a URL aponta para playlist,
    /// canal ou album. Em midia unica fica vazio.
    /// </summary>
    [JsonPropertyName("entries")]
    public List<MediaInfo> Entries { get; set; } = new();

    [JsonPropertyName("playlist_count")]
    public int? PlaylistCount { get; set; }

    [JsonIgnore]
    public bool IsPlaylist => Entries.Count > 0 || string.Equals(Type, "playlist", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public TimeSpan? DurationSpan => Duration is null ? null : TimeSpan.FromSeconds(Duration.Value);

    /// <summary>
    /// Formata a duracao no padrao h:mm:ss (ou m:ss quando abaixo de uma hora).
    /// Equivale ao _format_duration() do ultimate_downloader.py.
    /// </summary>
    public string FormatDuration()
    {
        if (DurationSpan is not { } d) return "--:--";
        return d.TotalHours >= 1
            ? $"{(int)d.TotalHours}:{d.Minutes:D2}:{d.Seconds:D2}"
            : $"{d.Minutes}:{d.Seconds:D2}";
    }

    /// <summary>
    /// Data de upload no formato YYYYMMDD do yt-dlp, convertida para DateOnly.
    /// </summary>
    public DateOnly? GetUploadDate()
    {
        if (string.IsNullOrWhiteSpace(UploadDate)) return null;
        return DateOnly.TryParseExact(UploadDate, "yyyyMMdd", out var d) ? d : null;
    }

    /// <summary>
    /// Devolve os formatos relevantes para exibicao, do melhor para o pior,
    /// sem os fragmentos de storyboard que o yt-dlp inclui.
    /// </summary>
    public IEnumerable<MediaFormat> GetDisplayableFormats() =>
        Formats
            .Where(f => f.FormatId is not null
                        && !string.Equals(f.Extension, "mhtml", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.Height ?? 0)
            .ThenByDescending(f => f.Bitrate ?? 0);

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Desserializa a saida de "yt-dlp --dump-single-json".
    /// </summary>
    public static MediaInfo? FromJson(string json) =>
        JsonSerializer.Deserialize<MediaInfo>(json, ParseOptions);
}

/// <summary>
/// Um formato disponivel (combinacao de codec, resolucao e bitrate).
/// </summary>
public sealed class MediaFormat
{
    [JsonPropertyName("format_id")]
    public string? FormatId { get; set; }

    [JsonPropertyName("format_note")]
    public string? FormatNote { get; set; }

    [JsonPropertyName("ext")]
    public string? Extension { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("fps")]
    public double? Fps { get; set; }

    [JsonPropertyName("vcodec")]
    public string? VideoCodec { get; set; }

    [JsonPropertyName("acodec")]
    public string? AudioCodec { get; set; }

    /// <summary>Bitrate total em kbps.</summary>
    [JsonPropertyName("tbr")]
    public double? Bitrate { get; set; }

    [JsonPropertyName("abr")]
    public double? AudioBitrate { get; set; }

    [JsonPropertyName("filesize")]
    public long? FileSize { get; set; }

    [JsonPropertyName("filesize_approx")]
    public long? FileSizeApprox { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonIgnore]
    public bool HasVideo => VideoCodec is not null && !VideoCodec.Equals("none", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool HasAudio => AudioCodec is not null && !AudioCodec.Equals("none", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public bool IsAudioOnly => HasAudio && !HasVideo;

    [JsonIgnore]
    public long? EffectiveSize => FileSize ?? FileSizeApprox;

    /// <summary>Rotulo curto para lista na interface, ex.: "1080p60 mp4 (H.264)".</summary>
    public string ToDisplayString()
    {
        if (IsAudioOnly)
        {
            var abr = AudioBitrate is { } a ? $"{a:0}kbps" : FormatNote ?? "audio";
            return $"{abr} {Extension} ({AudioCodec})";
        }

        var res = Height is { } h ? $"{h}p" : FormatNote ?? "?";
        var fps = Fps is { } f && f > 30 ? $"{f:0}" : string.Empty;
        return $"{res}{fps} {Extension} ({VideoCodec})";
    }
}
