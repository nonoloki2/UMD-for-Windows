using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using UMD.Core.Configuration;
using UMD.Core.Engine;
using UMD.Core.Models;
using UMD.Core.Platforms;
using UMD.Core.Queue;
using UMD.Core.Tools;

namespace UMD.App.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly AppSettings _settings;
    private readonly ToolManager _tools;
    private readonly YtDlpEngine _engine;
    private readonly DownloadQueue _queue;
    private readonly Dictionary<Guid, DownloadItemViewModel> _items = new();

    public MainViewModel()
    {
        _settings = AppSettings.Load();

        _tools = new ToolManager(
            configuredYtDlpPath: _settings.Tools.YtDlpPath,
            configuredFfmpegPath: _settings.Tools.FfmpegPath,
            configuredDenoPath: _settings.Tools.DenoPath);

        _engine = new YtDlpEngine(_tools);
        _queue = new DownloadQueue(_engine, _settings.Advanced.MaxConcurrentDownloads);
        _queue.JobUpdated += OnJobUpdated;

        _outputDirectory = _settings.Download.OutputDirectory;
        _audioOnly = _settings.Download.AudioOnly;
        _selectedQuality = _settings.Download.VideoQuality;
        _selectedAudioFormat = _settings.Download.AudioFormat;

        AddCommand = new RelayCommand(AddToQueue, () => PlatformDetector.IsValidUrl(Url));
        BrowseCommand = new RelayCommand(BrowseFolder);
        ClearFinishedCommand = new RelayCommand(ClearFinished);
        CancelAllCommand = new RelayCommand(() => _queue.CancelAll());
        OpenOutputCommand = new RelayCommand(OpenOutputFolder);

        CheckTools();
    }

    // ------------------------------------------------------------------
    // Fila
    // ------------------------------------------------------------------

    public ObservableCollection<DownloadItemViewModel> Items { get; } = new();

    public RelayCommand AddCommand { get; }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand ClearFinishedCommand { get; }
    public RelayCommand CancelAllCommand { get; }
    public RelayCommand OpenOutputCommand { get; }

    // ------------------------------------------------------------------
    // Campos ligados a tela
    // ------------------------------------------------------------------

    private string _url = string.Empty;
    public string Url
    {
        get => _url;
        set { if (Set(ref _url, value)) OnPropertyChanged(nameof(DetectedPlatform)); }
    }

    /// <summary>Mostra a plataforma enquanto o usuario digita, como confirmacao visual.</summary>
    public string DetectedPlatform =>
        string.IsNullOrWhiteSpace(Url) || !PlatformDetector.IsValidUrl(Url)
            ? string.Empty
            : PlatformDetector.GetDisplayName(PlatformDetector.Detect(Url));

    private string _outputDirectory;
    public string OutputDirectory
    {
        get => _outputDirectory;
        set
        {
            if (!Set(ref _outputDirectory, value)) return;
            _settings.Download.OutputDirectory = value;
            SaveSettings();
        }
    }

    private bool _audioOnly;
    public bool AudioOnly
    {
        get => _audioOnly;
        set
        {
            if (!Set(ref _audioOnly, value)) return;
            _settings.Download.AudioOnly = value;
            SaveSettings();
            OnPropertyChanged(nameof(IsVideoMode));
        }
    }

    public bool IsVideoMode => !AudioOnly;

    public IReadOnlyList<VideoQuality> Qualities { get; } = Enum.GetValues<VideoQuality>();

    private VideoQuality _selectedQuality;
    public VideoQuality SelectedQuality
    {
        get => _selectedQuality;
        set
        {
            if (!Set(ref _selectedQuality, value)) return;
            _settings.Download.VideoQuality = value;
            SaveSettings();
        }
    }

    public IReadOnlyList<AudioFormat> AudioFormats { get; } = Enum.GetValues<AudioFormat>();

    private AudioFormat _selectedAudioFormat;
    public AudioFormat SelectedAudioFormat
    {
        get => _selectedAudioFormat;
        set
        {
            if (!Set(ref _selectedAudioFormat, value)) return;
            _settings.Download.AudioFormat = value;
            SaveSettings();
        }
    }

    private string _toolStatus = string.Empty;
    public string ToolStatus
    {
        get => _toolStatus;
        private set => Set(ref _toolStatus, value);
    }

    private bool _toolsOk = true;
    public bool ToolsOk
    {
        get => _toolsOk;
        private set => Set(ref _toolsOk, value);
    }

    public string ActivityText
    {
        get
        {
            var ativos = Items.Count(i => i.IsActive);
            if (ativos == 0) return Items.Count == 0 ? "Fila vazia" : "Nada em andamento";
            return ativos == 1 ? "1 download em andamento" : $"{ativos} downloads em andamento";
        }
    }

    // ------------------------------------------------------------------
    // Acoes
    // ------------------------------------------------------------------

    private void AddToQueue()
    {
        var url = Url.Trim();
        if (!PlatformDetector.IsValidUrl(url)) return;

        var request = _settings.CreateRequest(
            url,
            audioOnly: AudioOnly,
            quality: SelectedQuality,
            outputDirectory: OutputDirectory) with
        {
            AudioFormat = SelectedAudioFormat
        };

        var job = _queue.Enqueue(request);

        var item = new DownloadItemViewModel(job);
        _items[job.Id] = item;
        Items.Insert(0, item);

        Url = string.Empty;
        OnPropertyChanged(nameof(ActivityText));
    }

    /// <summary>
    /// O nucleo dispara este evento a partir de uma thread de fundo. Tudo que
    /// toca a interface precisa voltar para a thread de UI.
    /// </summary>
    private void OnJobUpdated(DownloadJob job)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) return;

        dispatcher.InvokeAsync(() =>
        {
            if (_items.TryGetValue(job.Id, out var item))
                item.Refresh();

            OnPropertyChanged(nameof(ActivityText));
        });
    }

    private void ClearFinished()
    {
        _queue.ClearFinished();

        foreach (var item in Items.Where(i => !i.IsActive).ToList())
        {
            _items.Remove(item.Job.Id);
            Items.Remove(item);
        }

        OnPropertyChanged(nameof(ActivityText));
    }

    private void BrowseFolder()
    {
        // OpenFolderDialog existe no WPF a partir do .NET 8, entao nao precisamos
        // do velho FolderBrowserDialog do WinForms.
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Escolha a pasta de downloads",
            InitialDirectory = Directory.Exists(OutputDirectory) ? OutputDirectory : null
        };

        if (dialog.ShowDialog() == true)
            OutputDirectory = dialog.FolderName;
    }

    private void OpenOutputFolder()
    {
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(OutputDirectory)
            {
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // Abrir pasta nunca deve derrubar o app.
        }
    }

    private void CheckTools()
    {
        var status = _tools.CheckAll();
        ToolsOk = status.IsUsable && status.HasFfmpeg && status.HasDeno;
        ToolStatus = status.Describe();
    }

    private void SaveSettings()
    {
        try
        {
            _settings.Save();
        }
        catch (Exception)
        {
            // Nao poder gravar a configuracao nao justifica interromper o usuario.
        }
    }

    public void Dispose()
    {
        _queue.JobUpdated -= OnJobUpdated;
        _queue.Dispose();
    }
}