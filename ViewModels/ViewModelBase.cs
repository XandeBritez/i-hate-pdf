using CommunityToolkit.Mvvm.ComponentModel;

namespace IHatePdf.ViewModels;

/// <summary>Base das paginas: titulo, estado de ocupado e mensagem de status.</summary>
public abstract partial class ViewModelBase : ObservableObject
{
    protected ViewModelBase(string title, string glyph)
    {
        Title = title;
        Glyph = glyph;
    }

    public string Title { get; }

    /// <summary>Caractere Segoe MDL2 Assets usado como icone no menu lateral.</summary>
    public string Glyph { get; }

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    /// <summary>Recebe arquivos soltos na janela. Cada pagina filtra o que interessa.</summary>
    public abstract Task AcceptFilesAsync(IReadOnlyList<string> paths);
}
