using System.Diagnostics;

namespace UMD.Core.Tools;

/// <summary>
/// Localiza os binarios externos de que o app depende.
///
/// A ordem de busca e: pasta "tools" ao lado do executavel, depois caminho
/// configurado pelo usuario, depois PATH do sistema. Isso permite distribuir o
/// app com os binarios embutidos sem impedir que quem ja tem yt-dlp instalado
/// use o dele.
/// </summary>
public sealed class ToolManager
{
    private readonly string _baseDirectory;
    private readonly string? _configuredYtDlp;
    private readonly string? _configuredFfmpeg;
    private readonly string? _configuredDeno;

    private string? _ytDlpPath;
    private string? _ffmpegPath;
    private string? _denoPath;

    public ToolManager(string? baseDirectory = null,
                       string? configuredYtDlpPath = null,
                       string? configuredFfmpegPath = null,
                       string? configuredDenoPath = null)
    {
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        _configuredYtDlp = configuredYtDlpPath;
        _configuredFfmpeg = configuredFfmpegPath;
        _configuredDeno = configuredDenoPath;
    }

    /// <summary>Pasta onde os binarios acompanham o app.</summary>
    public string ToolsDirectory => Path.Combine(_baseDirectory, "tools");

    public string? YtDlpPath => _ytDlpPath ??= Resolve("yt-dlp.exe", _configuredYtDlp);

    public string? FfmpegPath => _ffmpegPath ??= Resolve("ffmpeg.exe", _configuredFfmpeg);

    /// <summary>
    /// Runtime de JavaScript. Desde o yt-dlp 2025.11.12 o YouTube exige que os
    /// desafios de JS (parametro "n" e cifra de assinatura) sejam resolvidos por
    /// um runtime externo. Sem ele o YouTube devolve "This video is not
    /// available". Nao afeta os outros sites.
    /// </summary>
    public string? DenoPath => _denoPath ??= Resolve("deno.exe", _configuredDeno);

    public bool IsYtDlpAvailable => YtDlpPath is not null;

    public bool IsFfmpegAvailable => FfmpegPath is not null;

    public bool IsDenoAvailable => DenoPath is not null;

    /// <summary>
    /// Diretorio do ffmpeg, que e o formato que o yt-dlp espera em
    /// --ffmpeg-location.
    /// </summary>
    public string? FfmpegDirectory =>
        FfmpegPath is { } p ? Path.GetDirectoryName(p) : null;

    private string? Resolve(string fileName, string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var bundled = Path.Combine(ToolsDirectory, fileName);
        if (File.Exists(bundled)) return bundled;

        var sideBySide = Path.Combine(_baseDirectory, fileName);
        if (File.Exists(sideBySide)) return sideBySide;

        return FindOnPath(fileName);
    }

    private static string? FindOnPath(string fileName)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path)) return null;

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // Entradas invalidas no PATH acontecem. Ignora e segue.
            }
        }

        return null;
    }

    /// <summary>
    /// Le a versao do yt-dlp. Serve tambem como teste de que o binario roda.
    /// </summary>
    public async Task<string?> GetYtDlpVersionAsync(CancellationToken ct = default)
    {
        if (YtDlpPath is not { } exe) return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            return process.ExitCode == 0 ? output.Trim() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Verifica o que falta antes de tentar qualquer download, para a interface
    /// poder avisar o usuario com clareza em vez de falhar no meio.
    /// </summary>
    public ToolStatus CheckAll() => new()
    {
        YtDlpPath = YtDlpPath,
        FfmpegPath = FfmpegPath,
        DenoPath = DenoPath
    };
}

public sealed class ToolStatus
{
    public string? YtDlpPath { get; init; }
    public string? FfmpegPath { get; init; }
    public string? DenoPath { get; init; }

    public bool HasYtDlp => YtDlpPath is not null;
    public bool HasFfmpeg => FfmpegPath is not null;
    public bool HasDeno => DenoPath is not null;

    /// <summary>
    /// Sem yt-dlp nada funciona. Sem ffmpeg da para baixar arquivos unicos, mas
    /// nao juntar video e audio separados nem converter para mp3, o que cobre a
    /// maioria dos casos reais.
    /// </summary>
    public bool IsUsable => HasYtDlp;

    public string Describe()
    {
        if (!HasYtDlp)
            return "yt-dlp.exe nao encontrado. Coloque-o na pasta 'tools' ao lado do executavel.";
        if (!HasFfmpeg)
            return "ffmpeg.exe nao encontrado. Downloads em alta qualidade e conversao para MP3 nao vao funcionar.";
        if (!HasDeno)
            return "deno.exe nao encontrado. O YouTube vai falhar com 'This video is not available'. Outros sites seguem funcionando.";
        return "Tudo pronto.";
    }
}