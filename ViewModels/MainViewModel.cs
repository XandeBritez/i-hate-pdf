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
    private readonly ISettingsService _settings;

    public MainViewModel(
        MergeViewModel merge,
        EditorViewModel editor,
        ConverterViewModel converter,
        CompressViewModel compress,
        PdfToWordViewModel pdfToWord,
        SecurityViewModel security,
        AboutViewModel about,
        IMessenger messenger,
        ISettingsService settings)
    {
        _messenger = messenger;
        _settings = settings;
        _theme = Enum.TryParse<AppTheme>(settings.Current.Theme, out var saved) ? saved : AppTheme.Light;
        Merge = merge;
        Editor = editor;
        Converter = converter;
        Compress = compress;
        PdfToWord = pdfToWord;
        Security = security;
        About = about;

        Pages = new ObservableCollection<ViewModelBase> { merge, editor, compress, converter, pdfToWord, security, about };
        _currentPage = merge;
    }

    public MergeViewModel Merge { get; }
    public EditorViewModel Editor { get; }
    public ConverterViewModel Converter { get; }
    public CompressViewModel Compress { get; }
    public PdfToWordViewModel PdfToWord { get; }
    public SecurityViewModel Security { get; }
    public AboutViewModel About { get; }

    public ObservableCollection<ViewModelBase> Pages { get; }

    [ObservableProperty] private ViewModelBase _currentPage;

    [ObservableProperty] private AppTheme _theme = AppTheme.Light;

    [ObservableProperty] private bool _isDragOver;

    partial void OnThemeChanged(AppTheme value)
    {
        _settings.Current.Theme = value.ToString();
        _settings.Save();

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
            // Comprimir e PDF -> Word so aceitam PDF; o resto vai para o conversor.
            CompressViewModel when !allPdf => Converter,
            SecurityViewModel when !allPdf => Converter,
            PdfToWordViewModel when !allPdf => Converter,
            AboutViewModel => allPdf ? Merge : (ViewModelBase)Converter,
            EditorViewModel when allPdf => Editor,
            ConverterViewModel when allPdf => (ViewModelBase)Merge,
            MergeViewModel when !allPdf => Converter,
            _ => CurrentPage
        };

        CurrentPage = target;
        await target.AcceptFilesAsync(files);
    }

    /// <summary>
    /// Abre o arquivo vindo do menu de contexto do Explorer na tela que o
    /// verbo pediu. Sem argumento reconhecido, o app abre normalmente.
    /// </summary>
    public async Task HandleCommandLineAsync(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return;

        var verb = args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : null;
        var files = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal) && File.Exists(a)).ToList();

        if (files.Count == 0) return;

        ViewModelBase target = verb switch
        {
            "--edit" => Editor,
            "--compress" => Compress,
            "--merge" => Merge,
            "--convert" => Converter,
            "--security" => Security,
            // Sem verbo, o proprio tipo do arquivo decide.
            _ => files.All(f => string.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase))
                ? Editor
                : Converter
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
