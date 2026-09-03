using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IHatePdf.Models;

/// <summary>Card de pagina exibido na grade do editor visual.</summary>
public partial class PageItem : ObservableObject
{
    public PageItem(string sourcePath, int pageIndex)
    {
        SourcePath = sourcePath;
        PageIndex = pageIndex;
    }

    /// <summary>PDF de onde a pagina veio (pode diferir do documento aberto apos "adicionar paginas").</summary>
    public string SourcePath { get; }

    /// <summary>Indice da pagina dentro de <see cref="SourcePath"/>.</summary>
    public int PageIndex { get; }

    /// <summary>Posicao 1..N na ordem atual do editor. Atualizada pelo ViewModel.</summary>
    [ObservableProperty] private int _displayNumber;

    /// <summary>Miniatura ja congelada (Freeze) para uso seguro na thread de UI.</summary>
    [ObservableProperty] private BitmapSource? _thumbnail;

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isLoading = true;

    /// <summary>true quando a pagina veio de um PDF diferente do documento aberto.</summary>
    [ObservableProperty] private bool _isFromOtherFile;

    public string SourceFileName => Path.GetFileName(SourcePath);

    public PageReference ToReference() => new(SourcePath, PageIndex);
}
