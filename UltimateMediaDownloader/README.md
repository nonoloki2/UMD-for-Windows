# Ultimate Media Downloader para Windows (.NET 10 + WPF)

Porte do [UMD](https://codeberg.org/nk2552003/umd) (Python/CLI, Linux) para uma
aplicação Windows nativa.

## Estado atual: Fase 1 — fundação do motor

O que já existe neste repositório:

| Arquivo | Substitui no original | Papel |
|---|---|---|
| `Engine/YtDlpEngine.cs` | `ultimate_downloader.py` (núcleo) | Executa o yt-dlp, lê progresso e resultado |
| `Engine/FormatSelector.cs` | `_get_format_selector()` | Monta a string de formato do yt-dlp |
| `Engine/ProgressParser.cs` | `progress_hook` | Lê o progresso do processo externo |
| `Models/MediaInfo.cs` | dicionário `info_dict` | Metadados tipados |
| `Models/DownloadRequest.cs` | 13 parâmetros soltos | Pedido de download |
| `Platforms/PlatformDetector.cs` | `utils/platform_utils.py` | Detecta a plataforma pela URL |
| `Queue/DownloadQueue.cs` | `download_batch_optimized()` | Fila com downloads simultâneos |
| `Tools/ToolManager.cs` | `scripts/install.sh` | Localiza yt-dlp.exe e ffmpeg.exe |
| `Configuration/AppSettings.cs` | `config.json` | Configurações em `%APPDATA%` |

Ainda **não** existe interface gráfica. Esta fase entrega o motor isolado, para
ser compilado e testado antes de construir a UI em cima.

## Como compilar

Pré-requisitos: .NET 10 SDK.

```powershell
cd UltimateMediaDownloader
dotnet build
```

Deve compilar sem nenhum pacote NuGet. O projeto não depende de nada externo
nesta fase.

## Como testar o motor

Crie um projeto de console temporário para exercitar a API:

```powershell
dotnet new console -o test/UMD.Sandbox
dotnet sln add test/UMD.Sandbox
dotnet add test/UMD.Sandbox reference src/UMD.Core
```

E em `Program.cs`:

```csharp
using UMD.Core.Configuration;
using UMD.Core.Engine;
using UMD.Core.Models;
using UMD.Core.Tools;

var tools = new ToolManager();
Console.WriteLine(tools.CheckAll().Describe());
Console.WriteLine($"yt-dlp: {await tools.GetYtDlpVersionAsync()}");

var engine = new YtDlpEngine(tools);

// 1. Ler metadados
var info = await engine.GetInfoAsync("https://www.youtube.com/watch?v=aqz-KE-bpKQ");
Console.WriteLine($"{info?.Title} | {info?.FormatDuration()} | {info?.Formats.Count} formatos");

// 2. Baixar com progresso
var settings = AppSettings.Load();
var request = settings.CreateRequest(
    "https://www.youtube.com/watch?v=aqz-KE-bpKQ",
    audioOnly: false,
    quality: VideoQuality.Q720p);

var progress = new Progress<DownloadProgress>(p =>
    Console.Write($"\r{p.Percent:0.0}% — {p.FormatSpeed()} — ETA {p.FormatEta()}   "));

var result = await engine.DownloadAsync(request, progress);
Console.WriteLine();
Console.WriteLine(result.Success
    ? $"OK: {string.Join(", ", result.FilePaths)}"
    : $"Falhou: {result.ErrorMessage}");
```

### Binários necessários

Baixe e coloque em `tools\` ao lado do executável compilado:

- `yt-dlp.exe` — https://github.com/yt-dlp/yt-dlp/releases (arquivo `yt-dlp.exe`)
- `ffmpeg.exe` — https://www.gyan.dev/ffmpeg/builds/ (build `essentials`)

Se você já tiver os dois no PATH, o `ToolManager` acha sozinho.

## Roadmap

**Fase 1 — motor (concluída)**
Execução do yt-dlp, metadados, seleção de formato, progresso, fila, configuração.

**Fase 2 — interface WPF**
Janela principal, campo de URL, seleção de qualidade e formato, lista da fila com
barras de progresso, pasta de saída, tela de configurações. Tema Fluent via WPF-UI.

**Fase 3 — qualidade de mídia**
Metadados e capa de álbum com TagLibSharp, conversão de formato, seleção de faixa
de áudio por idioma, legendas, seleção manual de formato.

**Fase 4 — plataformas de música**
Spotify, Apple Music, Amazon Music: leitura de metadados e busca do equivalente no
YouTube. Inclui o porte do `youtube_scorer.py` (1.138 linhas), que é o algoritmo
que escolhe o melhor resultado da busca.

**Fase 5 — scrapers próprios**
Plataformas que exigem raspagem de HTML própria, com AngleSharp no lugar do
BeautifulSoup. É a parte mais frágil do original e a que mais quebra quando os
sites mudam.

**Fase 6 — empacotamento**
Publicação self-contained em executável único, auto-atualização do yt-dlp,
instalador.

## Decisões de arquitetura

**Por que o yt-dlp roda como processo externo e não é reimplementado.**
O yt-dlp tem mais de 1.800 extractors mantidos por centenas de pessoas e é
atualizado quase toda semana para acompanhar mudanças nos sites. Reimplementar
isso em C# seria refazer um trabalho impossível de manter. O executável é
autocontido, roda no Windows sem Python instalado e pode ser atualizado sem
recompilar o app.

**Por que o progresso usa `--progress-template`.**
O original usava `progress_hook`, um callback Python chamado de dentro do
processo. Como aqui o yt-dlp é outro processo, a alternativa ingênua seria
extrair números da barra de progresso colorida, o que quebra a cada mudança de
formatação. O `--progress-template` faz o yt-dlp imprimir uma linha estruturada
feita para ser lida por máquina.

**Por que `ProcessStartInfo.ArgumentList` em vez de uma string única.**
Montar a linha de comando concatenando strings quebra com títulos que contenham
aspas ou espaços, e abre espaço para injeção de comando. O `ArgumentList` escapa
cada argumento individualmente.

## Licença

Apache 2.0, herdada do projeto original. Veja `LICENSE` e `NOTICE`.
