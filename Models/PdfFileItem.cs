using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IHatePdf.Models;

/// <summary>Arquivo PDF na fila de unificacao (merge).</summary>
public partial class PdfFileItem : ObservableObject
{
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private int _pageCount;
    [ObservableProperty] private long _sizeInBytes;

    /// <summary>Miniatura da primeira pagina, ja congelada (Freeze) pelo servico de render.</summary>
    [ObservableProperty] private BitmapSource? _thumbnail;

    [ObservableProperty] private bool _isLoading = true;

    /// <summary>Posicao 1..N na fila; e a ordem em que o arquivo entra no PDF final.</summary>
    [ObservableProperty] private int _displayNumber;

    public string FileName => Path.GetFileName(FilePath);

    public string DisplaySize => SizeInBytes switch
    {
        < 1024 => $"{SizeInBytes} B",
        < 1024 * 1024 => $"{SizeInBytes / 1024d:0.#} KB",
        _ => $"{SizeInBytes / (1024d * 1024d):0.#} MB"
    };

    partial void OnFilePathChanged(string value) => OnPropertyChanged(nameof(FileName));
    partial void OnSizeInBytesChanged(long value) => OnPropertyChanged(nameof(DisplaySize));
}
