using System.Text.Json;
using System.Text.Json.Serialization;
using UMD.Core.Models;

namespace UMD.Core.Configuration;

/// <summary>
/// Configuracoes do app, persistidas em JSON.
///
/// Espelha o config.json do UMD original, mas tipado. O arquivo fica em
/// %APPDATA%\UltimateMediaDownloader\settings.json, que e o lugar correto no
/// Windows (o original gravava ao lado do codigo, o que quebra quando o app
/// esta em Program Files).
/// </summary>
public sealed class AppSettings
{
    public DownloadSettings Download { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();
    public ToolSettings Tools { get; set; } = new();

    [JsonIgnore]
    public static string SettingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UltimateMediaDownloader");

    [JsonIgnore]
    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Configuracao corrompida nao pode impedir o app de abrir.
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, Options));
    }

    /// <summary>
    /// Monta um DownloadRequest usando os padroes salvos, sobrescrevendo so o
    /// que a tela pedir.
    /// </summary>
    public DownloadRequest CreateRequest(
        string url,
        bool? audioOnly = null,
        VideoQuality? quality = null,
        string? outputDirectory = null)
        => new()
        {
            Url = url,
            OutputDirectory = outputDirectory ?? Download.OutputDirectory,
            AudioOnly = audioOnly ?? Download.AudioOnly,
            Quality = quality ?? Download.VideoQuality,
            Container = Download.VideoContainer,
            AudioFormat = Download.AudioFormat,
            AudioQuality = Download.AudioQuality,
            EmbedMetadata = Download.EmbedMetadata,
            EmbedThumbnail = Download.EmbedThumbnail,
            DownloadSubtitles = Download.DownloadSubtitles,
            SubtitleLanguage = Download.SubtitleLanguage,
            OutputTemplate = Download.OutputTemplate,
            RateLimit = Advanced.RateLimit,
            Retries = Advanced.Retries,
            CookiesFromBrowser = Advanced.CookiesFromBrowser,
            ProxyUrl = Advanced.ProxyEnabled ? Advanced.ProxyUrl : null
        };
}

public sealed class DownloadSettings
{
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Downloads", "UltimateDownloader");

    public bool AudioOnly { get; set; }
    public VideoQuality VideoQuality { get; set; } = VideoQuality.Best;
    public VideoContainer VideoContainer { get; set; } = VideoContainer.Mp4;
    public AudioFormat AudioFormat { get; set; } = AudioFormat.Mp3;
    public AudioQuality AudioQuality { get; set; } = AudioQuality.Best;
    public bool EmbedMetadata { get; set; } = true;
    public bool EmbedThumbnail { get; set; } = true;
    public bool DownloadSubtitles { get; set; }
    public string SubtitleLanguage { get; set; } = "pt";
    public string OutputTemplate { get; set; } = "%(title).200B.%(ext)s";
}

public sealed class AdvancedSettings
{
    public int MaxConcurrentDownloads { get; set; } = 3;
    public int Retries { get; set; } = 10;
    public string? RateLimit { get; set; }
    public bool ProxyEnabled { get; set; }
    public string? ProxyUrl { get; set; }

    /// <summary>"chrome", "firefox", "edge" ou vazio para nao usar cookies.</summary>
    public string? CookiesFromBrowser { get; set; }
}

public sealed class ToolSettings
{
    /// <summary>Vazio = procurar automaticamente em tools\ e no PATH.</summary>
    public string? YtDlpPath { get; set; }
    public string? FfmpegPath { get; set; }

    /// <summary>Runtime de JavaScript exigido pelo YouTube.</summary>
    public string? DenoPath { get; set; }

    public bool AutoUpdateYtDlp { get; set; } = true;
}