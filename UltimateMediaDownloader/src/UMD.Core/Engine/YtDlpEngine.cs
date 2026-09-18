using System.Diagnostics;
using System.Text;
using UMD.Core.Models;
using UMD.Core.Platforms;
using UMD.Core.Tools;

namespace UMD.Core.Engine;

/// <summary>
/// Motor de download. Conversa com o yt-dlp.exe por linha de comando.
///
/// Este tipo substitui o nucleo de ultimate_downloader.py: os 4.595 linhas
/// daquele arquivo eram, na pratica, montagem do dicionario ydl_opts mais
/// impressao no terminal. Aqui a montagem virou BuildDownloadArguments() e a
/// impressao virou eventos que a interface consome.
///
/// Toda a saida do processo e lida de forma assincrona, linha a linha, para que
/// o progresso apareca em tempo real sem travar a UI.
/// </summary>
public sealed partial class YtDlpEngine
{
    private readonly ToolManager _tools;

    public YtDlpEngine(ToolManager tools) => _tools = tools;

    /// <summary>Linhas cruas de saida, para a janela de log.</summary>
    public event Action<string>? OutputReceived;


    /// <summary>
    /// Argumentos comuns a toda invocacao do yt-dlp.
    ///
    /// Desde o yt-dlp 2025.11.12 o YouTube exige um runtime de JavaScript
    /// externo para resolver os desafios de assinatura. Passamos o caminho
    /// explicitamente, do mesmo jeito que fazemos com o ffmpeg, em vez de
    /// depender do deno estar no PATH.
    /// </summary>
    private void AddCommonArguments(List<string> args)
    {
        if (_tools.DenoPath is { } deno)
        {
            args.Add("--js-runtimes");
            args.Add($"deno:{deno}");
        }
    }

    // ------------------------------------------------------------------
    // Consulta de metadados
    // ------------------------------------------------------------------

    /// <summary>
    /// Busca os metadados de uma URL sem baixar nada.
    /// Equivale ao get_video_info() do Python, mas sem o timeout manual com
    /// thread: aqui o CancellationToken resolve.
    /// </summary>
    /// <param name="flatPlaylist">
    /// Quando true, lista os itens da playlist sem consultar cada um. E muito
    /// mais rapido em playlists grandes e e o que a tela de selecao precisa.
    /// </param>
    /// <param name="noPlaylist">
    /// Quando true, ignora o parametro "list" da URL e le so a midia apontada.
    ///
    /// Isto e essencial: uma URL de video que carregue "&amp;list=RD..." (as radios
    /// automaticas do YouTube) descreve uma playlist sem fim. Sem esta flag o
    /// yt-dlp tenta extrair item por item e a consulta nunca termina.
    /// </param>
    public async Task<MediaInfo?> GetInfoAsync(
        string url,
        bool flatPlaylist = true,
        bool noPlaylist = false,
        CancellationToken ct = default)
    {
        EnsureYtDlp();

        var args = new List<string>
        {
            "--dump-single-json",
            "--no-warnings",
            "--no-progress",
            "--ignore-config",
            "--socket-timeout", "20"
        };

        AddCommonArguments(args);

        // As duas flags sao mutuamente exclusivas: --no-playlist descarta a
        // playlist inteira, --flat-playlist a mantem mas sem aprofundar.
        if (noPlaylist) args.Add("--no-playlist");
        else if (flatPlaylist) args.Add("--flat-playlist");

        args.Add(url);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var exitCode = await RunAsync(
            args,
            onStdout: line => stdout.AppendLine(line),
            onStderr: line => stderr.AppendLine(line),
            ct: ct).ConfigureAwait(false);

        if (exitCode != 0)
        {
            var message = stderr.ToString().Trim();
            throw new YtDlpException(
                string.IsNullOrEmpty(message) ? $"yt-dlp saiu com codigo {exitCode}." : message,
                exitCode);
        }

        var json = stdout.ToString().Trim();
        return string.IsNullOrEmpty(json) ? null : MediaInfo.FromJson(json);
    }

    // ------------------------------------------------------------------
    // Download
    // ------------------------------------------------------------------

    /// <summary>
    /// Executa um download completo, reportando progresso pelo IProgress.
    /// </summary>
    public async Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        EnsureYtDlp();
        Directory.CreateDirectory(request.OutputDirectory);

        var args = BuildDownloadArguments(request);
        var files = new List<string>();
        var announcedFiles = new List<string>();
        var raw = new StringBuilder();

        // Estado da contagem de faixas. Video em alta qualidade vem em DASH e o
        // yt-dlp baixa video e audio separadamente, cada um com progresso
        // proprio comecando do zero.
        var partIndex = 1;
        long previousPartsBytes = 0;
        string? currentFormatId = null;
        long lastDownloadedBytes = 0;

        void HandleLine(string line)
        {
            raw.AppendLine(line);
            OutputReceived?.Invoke(line);

            if (ProgressParser.TryParseFilePath(line) is { } path)
            {
                files.Add(path);
                return;
            }

            // Fontes secundarias: o yt-dlp anuncia o destino em linguagem natural
            // em varios pontos. Servem de rede de seguranca caso o --print falhe
            // ou devolva um caminho incompleto.
            if (ExtractAnnouncedPath(line) is { } announced)
                announcedFiles.Add(announced);

            if (ProgressParser.TryParse(line) is not { } p)
                return;

            // Uma faixa nova comeca quando o format_id muda, ou quando os bytes
            // baixados voltam para tras (o yt-dlp reinicia a contagem).
            var isNewPart = currentFormatId is not null
                            && (p.FormatId != currentFormatId || p.DownloadedBytes < lastDownloadedBytes);

            if (isNewPart)
            {
                partIndex++;
                previousPartsBytes += lastDownloadedBytes;
            }

            currentFormatId = p.FormatId;
            lastDownloadedBytes = p.DownloadedBytes;

            progress?.Report(p with
            {
                PartIndex = partIndex,
                PreviousPartsBytes = previousPartsBytes
            });
        }

        int exitCode;
        try
        {
            exitCode = await RunAsync(
                args,
                onStdout: HandleLine,
                onStderr: HandleLine,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return DownloadResult.Fail("Download cancelado pelo usuario.", -1, raw.ToString());
        }

        if (exitCode != 0)
        {
            return DownloadResult.Fail(
                ExtractError(raw.ToString()) ?? $"yt-dlp saiu com codigo {exitCode}.",
                exitCode,
                raw.ToString());
        }

        var resolved = ResolveFiles(files, announcedFiles, request.OutputDirectory);
        return DownloadResult.Ok(resolved, raw.ToString());
    }

    // ------------------------------------------------------------------
    // Montagem dos argumentos
    // ------------------------------------------------------------------

    /// <summary>
    /// Traduz um DownloadRequest para a linha de comando do yt-dlp.
    /// Deixado publico de proposito: facilita testar e permite mostrar ao
    /// usuario o comando exato que sera executado.
    /// </summary>
    public List<string> BuildDownloadArguments(DownloadRequest request)
    {
        var args = new List<string>
        {
            "--ignore-config",
            "--no-warnings",
            "--newline",
            "--progress",
            "--progress-template", ProgressParser.BuildTemplate(),
            "--print", ProgressParser.BuildFileTemplate(),
            "--no-simulate",
            "--retries", request.Retries.ToString(),
            "--fragment-retries", request.Retries.ToString(),
            "--concurrent-fragments", "4",
            "-f", FormatSelector.Build(request),
            "-P", request.OutputDirectory,
            "-o", request.OutputTemplate
        };

        AddCommonArguments(args);

        if (_tools.FfmpegDirectory is { } ffmpegDir)
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpegDir);
        }

        if (request.AudioOnly)
        {
            args.Add("--extract-audio");
            args.Add("--audio-format");
            args.Add(FormatSelector.ToExtension(request.AudioFormat));
            args.Add("--audio-quality");
            args.Add(FormatSelector.ToYtDlpAudioQuality(request.AudioQuality));
        }
        else if (FormatSelector.ToExtension(request.Container) is { } container)
        {
            args.Add("--merge-output-format");
            args.Add(container);
        }

        if (request.EmbedMetadata)
        {
            args.Add("--embed-metadata");
            // Grava artista, album e faixa quando o extractor souber, que e o
            // que o _enhance_audio_metadata() do Python fazia com mutagen.
            args.Add("--parse-metadata");
            args.Add("%(artist,uploader)s:%(meta_artist)s");
        }

        if (request.EmbedThumbnail)
        {
            args.Add("--embed-thumbnail");
            // Sem isto o yt-dlp falha em formatos que nao aceitam capa embutida
            // (wav e opus, por exemplo) e derruba o download inteiro.
            args.Add("--no-abort-on-error");
        }

        if (request.DownloadSubtitles)
        {
            args.Add("--write-subs");
            args.Add("--write-auto-subs");
            args.Add("--sub-langs");
            args.Add(request.SubtitleLanguage);
            args.Add("--embed-subs");
        }

        if (!string.IsNullOrWhiteSpace(request.AudioLanguage))
        {
            args.Add("--extractor-args");
            args.Add($"youtube:lang={request.AudioLanguage}");
        }

        switch (request.DownloadPlaylist)
        {
            case true:
                args.Add("--yes-playlist");
                break;
            case false:
                args.Add("--no-playlist");
                break;
            case null:
                // Sem escolha explicita, uma radio ou mix do YouTube ("list=RD...")
                // e tratada como video unico. Caso contrario o yt-dlp comecaria a
                // baixar uma playlist gerada sob demanda que nao tem fim.
                if (UrlHelper.HasPlaylistParameter(request.Url) && !UrlHelper.IsRealPlaylist(request.Url))
                    args.Add("--no-playlist");
                break;
        }

        // --playlist-items aceita intervalos no formato "5:20".
        if (request.PlaylistStartIndex > 1 || request.MaxDownloads is not null)
        {
            var start = Math.Max(1, request.PlaylistStartIndex);
            var end = request.MaxDownloads is { } max ? (start + max - 1).ToString() : string.Empty;
            args.Add("--playlist-items");
            args.Add($"{start}:{end}");
        }

        if (!string.IsNullOrWhiteSpace(request.RateLimit))
        {
            args.Add("--limit-rate");
            args.Add(request.RateLimit!);
        }

        if (!string.IsNullOrWhiteSpace(request.CookieFile) && File.Exists(request.CookieFile))
        {
            args.Add("--cookies");
            args.Add(request.CookieFile!);
        }
        else if (!string.IsNullOrWhiteSpace(request.CookiesFromBrowser))
        {
            args.Add("--cookies-from-browser");
            args.Add(request.CookiesFromBrowser!);
        }

        if (!string.IsNullOrWhiteSpace(request.ProxyUrl))
        {
            args.Add("--proxy");
            args.Add(request.ProxyUrl!);
        }

        args.Add(request.Url);
        return args;
    }


    // ------------------------------------------------------------------
    // Resolucao dos arquivos gerados
    // ------------------------------------------------------------------

    /// <summary>
    /// Linhas do yt-dlp que revelam o caminho de um arquivo.
    /// </summary>
    private static readonly (string Prefix, bool Quoted)[] PathAnnouncements =
    [
        ("[Merger] Merging formats into ", true),
        ("[download] Destination: ",       false),
        ("[ExtractAudio] Destination: ",   false),
        ("[FixupM3u8] Fixing ",            true)
    ];

    private static string? ExtractAnnouncedPath(string line)
    {
        var t = line.Trim();

        foreach (var (prefix, quoted) in PathAnnouncements)
        {
            if (!t.StartsWith(prefix, StringComparison.Ordinal)) continue;

            var value = t[prefix.Length..].Trim();
            if (quoted) value = value.Trim('"');

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        // "[download] X has already been downloaded"
        const string suffix = " has already been downloaded";
        if (t.StartsWith("[download] ", StringComparison.Ordinal) &&
            t.EndsWith(suffix, StringComparison.Ordinal))
        {
            return t["[download] ".Length..^suffix.Length].Trim();
        }

        return null;
    }

    /// <summary>
    /// Transforma os caminhos capturados em caminhos que realmente existem.
    ///
    /// O --print do yt-dlp pode devolver um caminho relativo, ou um caminho sem
    /// a extensao final quando o pos-processamento troca o container. Em vez de
    /// confiar cegamente, cada candidato e verificado no disco e, quando nao
    /// bate, procuramos na pasta de saida o arquivo com o mesmo nome base.
    /// </summary>
    private static List<string> ResolveFiles(
        IEnumerable<string> printed,
        IEnumerable<string> announced,
        string outputDirectory)
    {
        var result = new List<string>();

        foreach (var candidate in printed.Concat(announced))
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            var full = Path.IsPathRooted(candidate)
                ? candidate
                : Path.GetFullPath(Path.Combine(outputDirectory, candidate));

            if (File.Exists(full))
            {
                Add(full);
                continue;
            }

            if (FindByBaseName(full, outputDirectory) is { } found)
                Add(found);
        }

        return result;

        void Add(string path)
        {
            if (!result.Contains(path, StringComparer.OrdinalIgnoreCase))
                result.Add(path);
        }
    }

    /// <summary>
    /// Procura na pasta de saida um arquivo cujo nome, ignorando a extensao,
    /// seja igual ao do candidato. Resolve tanto o caso do caminho sem extensao
    /// quanto o da extensao trocada pelo pos-processamento (webm virando mp4,
    /// por exemplo). Havendo mais de um, fica com o mais recente.
    /// </summary>
    private static string? FindByBaseName(string candidate, string outputDirectory)
    {
        var dir = Path.GetDirectoryName(candidate);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            dir = outputDirectory;

        if (!Directory.Exists(dir)) return null;

        var name = Path.GetFileName(candidate);
        var stem = Path.GetFileNameWithoutExtension(candidate);

        try
        {
            return new DirectoryInfo(dir)
                .EnumerateFiles()
                .Where(f =>
                    // caminho veio sem extensao: "Titulo" casa com "Titulo.mp4"
                    string.Equals(Path.GetFileNameWithoutExtension(f.Name), name, StringComparison.OrdinalIgnoreCase)
                    // extensao trocada no pos-processamento
                    || string.Equals(Path.GetFileNameWithoutExtension(f.Name), stem, StringComparison.OrdinalIgnoreCase))
                // descarta os arquivos intermediarios do DASH: "Titulo.f137.mp4"
                .Where(f => !FragmentSuffix().IsMatch(Path.GetFileNameWithoutExtension(f.Name)))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault()?.FullName;
        }
        catch (Exception)
        {
            return null;
        }
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\.f\d+$")]
    private static partial System.Text.RegularExpressions.Regex FragmentSuffix();

    // ------------------------------------------------------------------
    // Execucao do processo
    // ------------------------------------------------------------------

    private async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        Action<string> onStdout,
        Action<string> onStderr,
        CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _tools.YtDlpPath!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        // ArgumentList escapa cada item automaticamente. Concatenar a mao numa
        // string unica quebra com titulos que tenham aspas ou espacos, um bug
        // classico deste tipo de app.
        foreach (var a in arguments) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        if (!process.Start())
            throw new YtDlpException("Nao foi possivel iniciar o yt-dlp.exe.", -1);

        var stdoutTask = PumpAsync(process.StandardOutput, onStdout, ct);
        var stderrTask = PumpAsync(process.StandardError, onStderr, ct);

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        return process.ExitCode;
    }

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine, CancellationToken ct)
    {
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
            onLine(line);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
            // O processo pode ter morrido sozinho entre a checagem e o Kill.
        }
    }

    private void EnsureYtDlp()
    {
        if (!_tools.IsYtDlpAvailable)
            throw new YtDlpException(_tools.CheckAll().Describe(), -1);
    }

    /// <summary>
    /// Pega a primeira linha de erro do yt-dlp, que e bem mais util para o
    /// usuario do que "codigo de saida 1".
    /// </summary>
    private static string? ExtractError(string output)
    {
        foreach (var line in output.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                return t[6..].Trim();
        }
        return null;
    }
}

public sealed class YtDlpException : Exception
{
    public int ExitCode { get; }

    public YtDlpException(string message, int exitCode) : base(message) => ExitCode = exitCode;
}