using System.Windows;

namespace UMD.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Uma excecao nao tratada numa thread de fundo fecharia o app sem
        // explicacao. Melhor mostrar e seguir em frente.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                args.Exception.Message,
                "Erro inesperado",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            args.Handled = true;
        };
    }
}