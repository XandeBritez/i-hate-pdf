using CommunityToolkit.Mvvm.ComponentModel;

namespace IHatePdf.Models;

public enum ConversionStatus { Pendente, Convertendo, Concluido, Erro }

/// <summary>Item da fila de conversao para PDF.</summary>
public partial class ConversionItem : ObservableObject
{
    public ConversionItem(string sourcePath) => SourcePath = sourcePath;

    public string SourcePath { get; }
    public string FileName => Path.GetFileName(SourcePath);
    public string Extension => Path.GetExtension(SourcePath).ToUpperInvariant().TrimStart('.');

    [ObservableProperty] private ConversionStatus _status = ConversionStatus.Pendente;
    [ObservableProperty] private string? _outputPath;
    [ObservableProperty] private string? _errorMessage;
}
