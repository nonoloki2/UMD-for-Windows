using System.Globalization;
using System.Windows;
using System.Windows.Data;
using UMD.Core.Models;

namespace UMD.App.Converters;

/// <summary>true vira Visible, false vira Collapsed. "inverter" como parametro troca.</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (parameter as string == "inverter") flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Texto vazio ou nulo vira Collapsed.</summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Traduz os enums do nucleo para portugues. Sem isto a lista mostraria
/// "Q1080p" e "Mp3", que sao nomes de codigo, nao de interface.
/// </summary>
public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        VideoQuality q => q switch
        {
            VideoQuality.Best => "Melhor disponivel",
            VideoQuality.Q2160p => "4K (2160p)",
            VideoQuality.Q1440p => "1440p",
            VideoQuality.Q1080p => "1080p (Full HD)",
            VideoQuality.Q720p => "720p (HD)",
            VideoQuality.Q480p => "480p",
            VideoQuality.Q360p => "360p",
            VideoQuality.Worst => "Menor tamanho",
            _ => q.ToString()
        },
        AudioFormat f => f switch
        {
            AudioFormat.Mp3 => "MP3",
            AudioFormat.Flac => "FLAC (sem perdas)",
            AudioFormat.Wav => "WAV (sem perdas)",
            AudioFormat.M4a => "M4A",
            AudioFormat.Opus => "Opus",
            AudioFormat.Aac => "AAC",
            _ => f.ToString()
        },
        _ => value?.ToString() ?? string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}