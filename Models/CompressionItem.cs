using CommunityToolkit.Mvvm.ComponentModel;

namespace IHatePdf.Models;

/// <summary>Item da fila de compressao, com o antes e o depois.</summary>
public partial class CompressionItem : ObservableObject
{
    public CompressionItem(string sourcePath)
    {
        SourcePath = sourcePath;
        OriginalBytes = new FileInfo(sourcePath).Length;
    }

    public string SourcePath { get; }
    public string FileName => Path.GetFileName(SourcePath);
    public long OriginalBytes { get; }

    [ObservableProperty] private ConversionStatus _status = ConversionStatus.Pendente;
    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private long _compressedBytes;
    [ObservableProperty] private double _savedPercent;

    /// <summary>false quando o arquivo ja estava otimizado e foi mantido como estava.</summary>
    [ObservableProperty] private bool _improved = true;

    public string DisplayOriginal => Format(OriginalBytes);
    public string DisplayCompressed => Format(CompressedBytes);

    /// <summary>Resumo do ganho, pronto para a UI.</summary>
    public string DisplaySaving => Status switch
    {
        ConversionStatus.Concluido when !Improved => "ja estava otimizado",
        ConversionStatus.Concluido => $"{DisplayOriginal} -> {DisplayCompressed} ({SavedPercent:0}% menor)",
        _ => DisplayOriginal
    };

    partial void OnCompressedBytesChanged(long value) => NotifyDisplay();
    partial void OnSavedPercentChanged(double value) => NotifyDisplay();
    partial void OnStatusChanged(ConversionStatus value) => NotifyDisplay();
    partial void OnImprovedChanged(bool value) => NotifyDisplay();

    private void NotifyDisplay()
    {
        OnPropertyChanged(nameof(DisplayCompressed));
        OnPropertyChanged(nameof(DisplaySaving));
    }

    private static string Format(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} KB",
        _ => $"{bytes / (1024d * 1024d):0.#} MB"
    };
}
