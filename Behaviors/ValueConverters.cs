using System.Globalization;
using System.Windows;
using System.Windows.Data;
using IHatePdf.Models;

namespace IHatePdf.Behaviors;

/// <summary>true -> Collapsed, false -> Visible.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Inverte um booleano (usado para IsEnabled durante operacoes longas).</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not true;
}

/// <summary>Colecao/valor vazio -> Visible (para placeholders de "solte arquivos aqui").</summary>
public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isEmpty = value switch
        {
            null => true,
            int count => count == 0,
            string text => string.IsNullOrWhiteSpace(text),
            System.Collections.ICollection collection => collection.Count == 0,
            _ => false
        };

        var invert = string.Equals(parameter as string, "invert", StringComparison.OrdinalIgnoreCase);
        var visible = invert ? !isEmpty : isEmpty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Compara o valor com o parametro (navegacao do menu lateral).</summary>
public sealed class EqualityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value, parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Status da conversao -> texto curto exibido no item da fila.</summary>
public sealed class ConversionStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ConversionStatus status
            ? status switch
            {
                ConversionStatus.Pendente => "Na fila",
                ConversionStatus.Convertendo => "Convertendo...",
                ConversionStatus.Concluido => "Concluido",
                ConversionStatus.Erro => "Erro",
                _ => string.Empty
            }
            : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Compara dois valores em um MultiBinding (item da navegacao x pagina atual).</summary>
public sealed class MultiEqualityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length == 2 && Equals(values[0], values[1]);

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
