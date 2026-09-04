using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHatePdf.Models;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>Fila de conversao TXT/DOCX/XLSX para PDF.</summary>
public sealed partial class ConverterViewModel : ViewModelBase
{
    private readonly IConversionService _conversionService;
    private readonly IImageToPdfService _imageService;
    private readonly IImageThumbnailService _thumbnailService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settings;

    public ConverterViewModel(
        IConversionService conversionService,
        IImageToPdfService imageService,
        IImageThumbnailService thumbnailService,
        IDialogService dialogService,
        ISettingsService settings)
        : base("Converter para PDF", "")
    {
        _conversionService = conversionService;
        _imageService = imageService;
        _thumbnailService = thumbnailService;
        _dialogService = dialogService;
        _settings = settings;

        OutputFolder = settings.Current.ConverterOutputFolder ?? AppSettings.DefaultOutputFolder;
        CombineImages = settings.Current.CombineImages;

        Items.CollectionChanged += OnItemsChanged;
    }

    public ObservableCollection<ConversionItem> Items { get; } = new();

    [ObservableProperty] private string _outputFolder = string.Empty;

    /// <summary>Quando ligado, todas as imagens da fila viram um unico PDF.</summary>
    [ObservableProperty] private bool _combineImages;

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A ordem da fila e a ordem das paginas no PDF unico: renumera a cada
        // insercao, remocao ou arrasto.
        for (var i = 0; i < Items.Count; i++)
            Items[i].DisplayNumber = i + 1;

        ConvertAllCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(ShowList));
    }

    partial void OnOutputFolderChanged(string value)
    {
        _settings.Current.ConverterOutputFolder = value;
        _settings.Save();
    }

    partial void OnCombineImagesChanged(bool value)
    {
        _settings.Current.CombineImages = value;
        _settings.Save();

        OnPropertyChanged(nameof(ShowGrid));
        OnPropertyChanged(nameof(ShowList));
    }

    /// <summary>A opcao de juntar so aparece quando ha imagem na fila.</summary>
    public bool HasImages => Items.Any(i => _imageService.IsImage(i.SourcePath));

    /// <summary>No modo album a ordem importa, entao a fila vira grade arrastavel.</summary>
    public bool ShowGrid => CombineImages && Items.Count > 0;

    public bool ShowList => !CombineImages && Items.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private ConversionItem? _selectedItem;

    /// <summary>Filtro de dialogo montado a partir das extensoes que o servico aceita.</summary>
    public string OpenFilter =>
        "Documentos suportados|" +
        string.Join(";", _conversionService.SupportedExtensions.Select(e => "*" + e)) +
        "|Todos os arquivos (*.*)|*.*";

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var paths = _dialogService.OpenFiles("Selecione os arquivos", OpenFilter);
        if (paths is not null)
            await AcceptFilesAsync(paths);
    }

    public override async Task AcceptFilesAsync(IReadOnlyList<string> paths)
    {
        var ignored = 0;

        foreach (var path in paths)
        {
            if (!_conversionService.CanConvert(path))
            {
                ignored++;
                continue;
            }

            if (Items.Any(i => string.Equals(i.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            Items.Add(new ConversionItem(path));
        }

        StatusMessage = ignored > 0
            ? $"{Items.Count} arquivo(s) na fila. {ignored} ignorado(s) por extensao nao suportada."
            : $"{Items.Count} arquivo(s) na fila.";

        await LoadThumbnailsAsync();
    }

    /// <summary>Carrega as miniaturas das imagens ainda sem uma.</summary>
    private async Task LoadThumbnailsAsync()
    {
        foreach (var item in Items.Where(i => i.Thumbnail is null && _imageService.IsImage(i.SourcePath)).ToList())
        {
            try
            {
                item.Thumbnail = await _thumbnailService.RenderAsync(item.SourcePath);
            }
            catch (Exception)
            {
                // Imagem ilegivel continua na fila; o card so fica sem previa.
            }
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        if (SelectedItem is not null)
            Items.Remove(SelectedItem);
    }

    private bool HasSelection() => SelectedItem is not null;

    [RelayCommand]
    private void Clear()
    {
        Items.Clear();
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private void ChooseOutputFolder()
    {
        var folder = _dialogService.PickFolder("Pasta de saida");
        if (folder is not null)
            OutputFolder = folder;
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (!Directory.Exists(OutputFolder)) return;

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = OutputFolder,
            UseShellExecute = true
        });
    }

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAllAsync()
    {
        try
        {
            IsBusy = true;
            Directory.CreateDirectory(OutputFolder);

            var ok = 0;
            var failed = 0;

            var pending = Items.Where(i => i.Status != ConversionStatus.Concluido).ToList();

            // Modo album: as imagens saem em um PDF unico, na ordem da fila.
            if (CombineImages)
            {
                var images = pending.Where(i => _imageService.IsImage(i.SourcePath)).ToList();
                if (images.Count > 0)
                {
                    var album = Path.Combine(OutputFolder, "imagens.pdf");

                    foreach (var image in images)
                        image.Status = ConversionStatus.Convertendo;

                    StatusMessage = $"Juntando {images.Count} imagem(ns) em um PDF...";

                    try
                    {
                        await _imageService.CombineAsync(images.Select(i => i.SourcePath), album);

                        foreach (var image in images)
                        {
                            image.OutputPath = album;
                            image.Status = ConversionStatus.Concluido;
                        }

                        ok += images.Count;
                    }
                    catch (Exception ex)
                    {
                        foreach (var image in images)
                        {
                            image.Status = ConversionStatus.Erro;
                            image.ErrorMessage = ex.Message;
                        }

                        failed += images.Count;
                    }

                    pending = pending.Except(images).ToList();
                }
            }

            foreach (var item in pending)
            {
                item.Status = ConversionStatus.Convertendo;
                item.ErrorMessage = null;
                StatusMessage = $"Convertendo {item.FileName}...";

                var output = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(item.SourcePath) + ".pdf");

                try
                {
                    item.OutputPath = await _conversionService.ConvertToPdfAsync(item.SourcePath, output);
                    item.Status = ConversionStatus.Concluido;
                    ok++;
                }
                catch (Exception ex)
                {
                    item.Status = ConversionStatus.Erro;
                    item.ErrorMessage = ex.Message;
                    failed++;
                }
            }

            StatusMessage = failed == 0
                ? $"{ok} arquivo(s) convertido(s) em {OutputFolder}"
                : $"{ok} convertido(s), {failed} com erro. Passe o mouse sobre o item para ver o motivo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanConvert() => Items.Count > 0;
}
