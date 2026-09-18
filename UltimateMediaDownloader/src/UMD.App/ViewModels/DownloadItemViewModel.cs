using System.Diagnostics;
using System.IO;
using UMD.Core.Models;
using UMD.Core.Platforms;
using UMD.Core.Queue;

namespace UMD.App.ViewModels;

/// <summary>
/// Um item da fila, na forma que a tela precisa.
///
/// O DownloadJob do nucleo nao notifica mudancas: ele e um objeto de dados puro,
/// sem nada de interface. Esta classe faz a ponte, traduzindo estado em texto,
/// percentual e visibilidade de botoes.
/// </summary>
public sealed class DownloadItemViewModel : ObservableObject
{
    public DownloadJob Job { get; }

    public DownloadItemViewModel(DownloadJob job)
    {
        Job = job;

        OpenFolderCommand = new RelayCommand(OpenFolder, () => HasFile);
        PlayCommand = new RelayCommand(Play, () => HasFile);
        CancelCommand = new RelayCommand(() => Job.Cancel(), () => IsActive);
    }

    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand PlayCommand { get; }
    public RelayCommand CancelCommand { get; }

    public string Title => Job.Title;

    public string PlatformName => PlatformDetector.GetDisplayName(Job.Platform);

    public bool IsActive => Job.Status is JobStatus.Pending or JobStatus.FetchingInfo
                                        or JobStatus.Downloading or JobStatus.PostProcessing;

    public bool HasFile => Job.Result?.FilePaths.Count > 0;

    public string? FilePath => Job.Result?.FilePaths.FirstOrDefault();

    /// <summary>Percentual da faixa atual, de 0 a 100.</summary>
    public double Percent => Job.Progress?.Percent ?? 0;

    /// <summary>
    /// A barra fica indeterminada quando o total ainda nao e conhecido: durante
    /// a busca de metadados e em transmissoes sem tamanho declarado.
    /// </summary>
    public bool IsIndeterminate =>
        Job.Status is JobStatus.FetchingInfo or JobStatus.PostProcessing
        || (Job.Status == JobStatus.Downloading && Job.Progress?.Percent is null);

    public string StatusText => Job.Status switch
    {
        JobStatus.Pending => "Na fila",
        JobStatus.FetchingInfo => "Lendo informacoes...",
        JobStatus.Downloading => DescribeDownload(),
        JobStatus.PostProcessing => "Processando (ffmpeg)...",
        JobStatus.Completed => HasFile ? Path.GetFileName(FilePath) ?? "Concluido" : "Concluido",
        JobStatus.Failed => "Falhou: " + (Job.ErrorMessage ?? "erro desconhecido"),
        JobStatus.Cancelled => "Cancelado",
        _ => string.Empty
    };

    /// <summary>
    /// Linha secundaria com velocidade, ETA e acumulado. Vazia quando nao ha
    /// download em andamento.
    /// </summary>
    public string DetailText
    {
        get
        {
            if (Job.Progress is not { } p || Job.Status != JobStatus.Downloading)
                return string.Empty;

            var parts = new List<string>();

            if (p.TotalBytes is { } total)
                parts.Add($"{DownloadProgress.FormatBytes(p.DownloadedBytes)} de {DownloadProgress.FormatBytes(total)}");
            else
                parts.Add(DownloadProgress.FormatBytes(p.DownloadedBytes));

            parts.Add(p.FormatSpeed());

            if (p.EtaSeconds is > 0) parts.Add("faltam " + p.FormatEta());

            // Video em alta qualidade vem em DASH: faixa 1 video, faixa 2 audio.
            if (p.PartIndex > 1)
                parts.Add($"total {DownloadProgress.FormatBytes(p.TotalDownloadedBytes)}");

            return string.Join("   |   ", parts);
        }
    }

    public string StatusColorKey => Job.Status switch
    {
        JobStatus.Completed => "SuccessBrush",
        JobStatus.Failed => "ErrorBrush",
        JobStatus.Cancelled => "MutedBrush",
        _ => "AccentBrush"
    };

    private string DescribeDownload()
    {
        var p = Job.Progress;
        if (p is null) return "Baixando...";

        return p.PartIndex > 1 ? $"Baixando audio ({p.PartIndex} de 2)" : "Baixando video";
    }

    /// <summary>Chamado pela tela quando o nucleo avisa que o job mudou.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DetailText));
        OnPropertyChanged(nameof(Percent));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(StatusColorKey));
    }

    private void OpenFolder()
    {
        if (FilePath is not { } path) return;

        try
        {
            // /select deixa o arquivo ja destacado no Explorer.
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Abrir o Explorer nunca deve derrubar o app.
        }
    }

    private void Play()
    {
        if (FilePath is not { } path || !File.Exists(path)) return;

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Sem player associado, nao ha o que fazer.
        }
    }
}