using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHatePdf.Models;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>Fila de conversao TXT/DOCX/XLSX para PDF.</summary>
public sealed partial class ConverterViewModel : ViewModelBase
{
    private readonly IConversionService _conversionService;
    private readonly IDialogService _dialogService;

    public ConverterViewModel(IConversionService conversionService, IDialogService dialogService)
        : base("Converter para PDF", "")
    {
        _conversionService = conversionService;
        _dialogService = dialogService;

        OutputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IHatePdf");

        Items.CollectionChanged += (_, _) => ConvertAllCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<ConversionItem> Items { get; } = new();

    [ObservableProperty] private string _outputFolder = string.Empty;

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

    public override Task AcceptFilesAsync(IReadOnlyList<string> paths)
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

        return Task.CompletedTask;
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

            foreach (var item in Items.Where(i => i.Status != ConversionStatus.Concluido))
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
