using System.Text.RegularExpressions;

namespace UMD.Core.Platforms;

/// <summary>
/// Plataformas que o app reconhece de forma explicita. Qualquer outra coisa cai
/// em Generic e vai direto para o yt-dlp, que sozinho cobre mais de mil sites.
/// </summary>
public enum Platform
{
    Generic,
    YouTube,
    YouTubeMusic,
    Spotify,
    AppleMusic,
    AmazonMusic,
    SoundCloud,
    JioSaavn,
    Gaana,
    Boomplay,
    Audiomack,
    Bandcamp,
    Instagram,
    TikTok,
    Twitter,
    Facebook,
    Reddit,
    Pinterest,
    Tumblr,
    LinkedIn,
    Vimeo,
    Dailymotion,
    Twitch,
    Kick,
    Rumble,
    BitChute,
    PeerTube,
    Ted,
    Veoh,
    Flickr,
    TrillerTv,
    FourKWallpapers
}

/// <summary>
/// Como a plataforma deve ser tratada.
/// </summary>
public enum HandlingStrategy
{
    /// <summary>yt-dlp resolve sozinho. E o caminho da grande maioria.</summary>
    Direct,

    /// <summary>
    /// A plataforma nao entrega o arquivo. O app le os metadados (artista,
    /// faixa, album), procura o equivalente no YouTube e baixa de la.
    /// E o que o UMD original faz com Spotify, Apple Music, JioSaavn e Gaana.
    /// </summary>
    MetadataThenYouTube,

    /// <summary>Exige scraping proprio do HTML do site.</summary>
    CustomScraper
}

/// <summary>
/// Descobre a plataforma a partir da URL.
///
/// Porte de detect_platform() de utils/platform_utils.py (linha 251). O original
/// era uma cadeia de if/elif com "substring in url" solto, que dava falso
/// positivo em URLs com o nome do site no caminho ou na query. Aqui a
/// comparacao e feita sobre o host ja parseado.
/// </summary>
public static class PlatformDetector
{
    private static readonly Regex XHamsterPattern = new(
        @"^(xhamster|xhwebsite|xhofficial|xhlocal|xhopen|xhtotal|megaxh|xhwide|xhtab|xhtime)\d*\.",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Host exato ou sufixo de dominio para cada plataforma.</summary>
    private static readonly (string Domain, Platform Platform)[] DomainMap =
    [
        ("music.youtube.com", Platform.YouTubeMusic),
        ("youtube.com",       Platform.YouTube),
        ("youtu.be",          Platform.YouTube),
        ("spotify.com",       Platform.Spotify),
        ("music.apple.com",   Platform.AppleMusic),
        ("itunes.apple.com",  Platform.AppleMusic),
        ("music.amazon.com",  Platform.AmazonMusic),
        ("soundcloud.com",    Platform.SoundCloud),
        ("jiosaavn.com",      Platform.JioSaavn),
        ("jiosvn.com",        Platform.JioSaavn),
        ("gaana.com",         Platform.Gaana),
        ("boomplay.com",      Platform.Boomplay),
        ("audiomack.com",     Platform.Audiomack),
        ("bandcamp.com",      Platform.Bandcamp),
        ("instagram.com",     Platform.Instagram),
        ("tiktok.com",        Platform.TikTok),
        ("twitter.com",       Platform.Twitter),
        ("x.com",             Platform.Twitter),
        ("facebook.com",      Platform.Facebook),
        ("fb.watch",          Platform.Facebook),
        ("reddit.com",        Platform.Reddit),
        ("redd.it",           Platform.Reddit),
        ("pinterest.com",     Platform.Pinterest),
        ("pin.it",            Platform.Pinterest),
        ("tumblr.com",        Platform.Tumblr),
        ("linkedin.com",      Platform.LinkedIn),
        ("vimeo.com",         Platform.Vimeo),
        ("dailymotion.com",   Platform.Dailymotion),
        ("dai.ly",            Platform.Dailymotion),
        ("twitch.tv",         Platform.Twitch),
        ("kick.com",          Platform.Kick),
        ("rumble.com",        Platform.Rumble),
        ("bitchute.com",      Platform.BitChute),
        ("ted.com",           Platform.Ted),
        ("veoh.com",          Platform.Veoh),
        ("flickr.com",        Platform.Flickr),
        ("flic.kr",           Platform.Flickr),
        ("trillertv.com",     Platform.TrillerTv),
        ("fite.tv",           Platform.TrillerTv),
        ("4kwallpapers.com",  Platform.FourKWallpapers)
    ];

    public static Platform Detect(string url)
    {
        var host = GetHost(url);
        if (host is null) return Platform.Generic;

        foreach (var (domain, platform) in DomainMap)
        {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return platform;
            }
        }

        if (XHamsterPattern.IsMatch(host)) return Platform.Generic;

        return Platform.Generic;
    }

    /// <summary>
    /// Define como cada plataforma deve ser tratada pelo orquestrador.
    /// </summary>
    public static HandlingStrategy GetStrategy(Platform platform) => platform switch
    {
        Platform.Spotify
            or Platform.AppleMusic
            or Platform.AmazonMusic
            or Platform.JioSaavn
            or Platform.Gaana
            or Platform.Boomplay => HandlingStrategy.MetadataThenYouTube,

        Platform.Pinterest
            or Platform.FourKWallpapers => HandlingStrategy.CustomScraper,

        _ => HandlingStrategy.Direct
    };

    /// <summary>Nome amigavel para exibir na interface.</summary>
    public static string GetDisplayName(Platform platform) => platform switch
    {
        Platform.YouTube => "YouTube",
        Platform.YouTubeMusic => "YouTube Music",
        Platform.AppleMusic => "Apple Music",
        Platform.AmazonMusic => "Amazon Music",
        Platform.SoundCloud => "SoundCloud",
        Platform.JioSaavn => "JioSaavn",
        Platform.BitChute => "BitChute",
        Platform.PeerTube => "PeerTube",
        Platform.TrillerTv => "TrillerTV",
        Platform.FourKWallpapers => "4K Wallpapers",
        Platform.LinkedIn => "LinkedIn",
        Platform.TikTok => "TikTok",
        Platform.Ted => "TED",
        Platform.Generic => "Generico",
        _ => platform.ToString()
    };

    /// <summary>
    /// Valida a URL e devolve o host em minusculas, sem "www.".
    /// </summary>
    private static string? GetHost(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var candidate = url.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
            candidate = "https://" + candidate;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    /// <summary>
    /// Checagem basica de URL, equivalente ao utils/url_validator.py.
    /// </summary>
    public static bool IsValidUrl(string url) => GetHost(url) is not null;
}
