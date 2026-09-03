using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>Mensagem de troca de tema publicada no WeakReferenceMessenger.</summary>
public sealed class ThemeChangedMessage : ValueChangedMessage<AppTheme>
{
    public ThemeChangedMessage(AppTheme value) : base(value) { }
}

public enum AppTheme { Light, Dark }

/// <summary>
/// Navegacao entre paginas, tema e roteamento do drag and drop global:
/// os arquivos soltos na janela vao para a pagina ativa, e PDFs soltos na
/// tela de conversao sao redirecionados para a tela de uniao.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IMessenger _messenger;

    public MainViewModel(
        MergeViewModel merge,
        EditorViewModel editor,
        ConverterViewModel converter,
        AboutViewModel about,
        IMessenger messenger)
    {
        _messenger = messenger;
        Merge = merge;
        Editor = editor;
        Converter = converter;
        About = about;

        Pages = new ObservableCollection<ViewModelBase> { merge, editor, converter, about };
        _currentPage = merge;
    }

    public MergeViewModel Merge { get; }
    public EditorViewModel Editor { get; }
    public ConverterViewModel Converter { get; }
    public AboutViewModel About { get; }

    public ObservableCollection<ViewModelBase> Pages { get; }

    [ObservableProperty] private ViewModelBase _currentPage;

    [ObservableProperty] private AppTheme _theme = AppTheme.Light;

    [ObservableProperty] private bool _isDragOver;

    partial void OnThemeChanged(AppTheme value)
    {
        _messenger.Send(new ThemeChangedMessage(value));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    public bool IsDarkTheme => Theme == AppTheme.Dark;

    /// <summary>Recebe o estado de arraste vindo do FileDropBehavior (View -> VM).</summary>
    [RelayCommand]
    private void SetDragOver(bool value) => IsDragOver = value;

    [RelayCommand]
    private void ToggleTheme() => Theme = Theme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;

    [RelayCommand]
    private void Navigate(ViewModelBase page) => CurrentPage = page;

    /// <summary>
    /// Ponto unico de entrada do drag and drop da janela.
    /// Escolhe a pagina de destino pela extensao dos arquivos soltos.
    /// </summary>
    [RelayCommand]
    public async Task DropFilesAsync(IReadOnlyList<string>? paths)
    {
        IsDragOver = false;
        if (paths is null || paths.Count == 0) return;

        var files = ExpandDirectories(paths);
        if (files.Count == 0) return;

        var allPdf = files.All(f => string.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase));

        // O editor trabalha com um documento por vez: so recebe o drop se ja estiver ativo.
        var target = CurrentPage switch
        {
            AboutViewModel => allPdf ? Merge : (ViewModelBase)Converter,
            EditorViewModel when allPdf => Editor,
            ConverterViewModel when allPdf => (ViewModelBase)Merge,
            MergeViewModel when !allPdf => Converter,
            _ => CurrentPage
        };

        CurrentPage = target;
        await target.AcceptFilesAsync(files);
    }

    /// <summary>Aceita pastas soltas expandindo o primeiro nivel de arquivos.</summary>
    private static List<string> ExpandDirectories(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                result.AddRange(Directory.EnumerateFiles(path));
            else if (File.Exists(path))
                result.Add(path);
        }
        return result;
    }
}
