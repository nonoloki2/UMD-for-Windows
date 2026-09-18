using System.Windows;
using UMD.App.ViewModels;

namespace UMD.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Closed += (_, _) => (DataContext as MainViewModel)?.Dispose();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    // Os dois RadioButton controlam a mesma propriedade em sentidos opostos.
    // Um binding TwoWay em cada um geraria disparos cruzados na inicializacao,
    // entao o estado e escrito explicitamente aqui.
    private void VideoMode_Checked(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.AudioOnly = false;
    }

    private void AudioMode_Checked(object sender, RoutedEventArgs e)
    {
        if (Vm is { } vm) vm.AudioOnly = true;
    }
}