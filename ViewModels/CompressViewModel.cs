using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHatePdf.Models;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>Fila de PDFs a reduzir, com o antes e o depois de cada arquivo.</summary>
public sealed partial class CompressViewModel : ViewModelBase
{
    private const string PdfFilter = "Documentos PDF (*.pdf)|*.pdf";

    private readonly IPdfCompressionService _compressionService;
    private readonly IDialogService _dialogService;

    public CompressViewModel(IPdfCompressionService compressionService, IDialogService dialogService)
        : base("Comprimir PDF", "")
    {
        _compressionService = compressionService;
        _dialogService = dialogService;

        OutputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IHatePdf");

        Items.CollectionChanged += (_, _) => CompressAllCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<CompressionItem> Items { get; } = new();

    [ObservableProperty] private string _outputFolder = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private CompressionItem? _selectedItem;

    /// <summary>Rasterizar e o modo que realmente encolhe; comeca desligado por ser destrutivo.</summary>
    [ObservableProperty] private bool _rasterize;

    [ObservableProperty] private CompressionStrength _strength = CompressionStrength.Balanced;

    [ObservableProperty] private double _progress;

    /// <summary>Quantos arquivos da fila perderiam o texto selecionavel ao rasterizar.</summary>
    [ObservableProperty] private int _filesWithText;

    public bool ShowTextWarning => Rasterize && FilesWithText > 0;

    partial void OnRasterizeChanged(bool value) => OnPropertyChanged(nameof(ShowTextWarning));
    partial void OnFilesWithTextChanged(int value) => OnPropertyChanged(nameof(ShowTextWarning));

    public bool StrengthHigh
    {
        get => Strength == CompressionStrength.High;
        set { if (value) Strength = CompressionStrength.High; }
    }

    public bool StrengthBalanced
    {
        get => Strength == CompressionStrength.Balanced;
        set { if (value) Strength = CompressionStrength.Balanced; }
    }

    public bool StrengthMaximum
    {
        get => Strength == CompressionStrength.Maximum;
        set { if (value) Strength = CompressionStrength.Maximum; }
    }

    partial void OnStrengthChanged(CompressionStrength value)
    {
        OnPropertyChanged(nameof(StrengthHigh));
        OnPropertyChanged(nameof(StrengthBalanced));
        OnPropertyChanged(nameof(StrengthMaximum));
    }

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var paths = _dialogService.OpenFiles("Selecione os PDFs", PdfFilter);
        if (paths is not null)
            await AcceptFilesAsync(paths);
    }

    public override async Task AcceptFilesAsync(IReadOnlyList<string> paths)
    {
        var ignored = 0;

        foreach (var path in paths)
        {
            if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                ignored++;
                continue;
            }

            if (Items.Any(i => string.Equals(i.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            try
            {
                Items.Add(new CompressionItem(path));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Arquivo invalido", $"'{Path.GetFileName(path)}': {ex.Message}");
            }
        }

        StatusMessage = ignored > 0
            ? $"{Items.Count} PDF(s) na fila. {ignored} ignorado(s): esta tela aceita apenas PDF."
            : $"{Items.Count} PDF(s) na fila.";

        await UpdateTextCountAsync();
    }

    /// <summary>Descobre quantos arquivos tem texto, para avisar antes de rasterizar.</summary>
    private async Task UpdateTextCountAsync()
    {
        var count = 0;
        foreach (var item in Items)
        {
            if (await _compressionService.HasExtractableTextAsync(item.SourcePath))
                count++;
        }

        FilesWithText = count;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RemoveAsync()
    {
        if (SelectedItem is null) return;

        Items.Remove(SelectedItem);
        await UpdateTextCountAsync();
    }

    private bool HasSelection() => SelectedItem is not null;

    [RelayCommand]
    private void Clear()
    {
        Items.Clear();
        FilesWithText = 0;
        StatusMessage = string.Empty;
        Progress = 0;
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

    [RelayCommand(CanExecute = nameof(CanCompress))]
    private async Task CompressAllAsync()
    {
        try
        {
            IsBusy = true;
            Progress = 0;
            Directory.CreateDirectory(OutputFolder);

            var mode = Rasterize ? CompressionMode.Rasterize : CompressionMode.Lossless;
            var pending = Items.Where(i => i.Status != ConversionStatus.Concluido).ToList();

            long totalBefore = 0, totalAfter = 0;
            var done = 0;
            var failed = 0;

            foreach (var item in pending)
            {
                item.Status = ConversionStatus.Convertendo;
                item.ErrorMessage = null;
                StatusMessage = $"Comprimindo {item.FileName}...";

                // Sufixo no nome: comprimir nunca sobrescreve o original.
                var output = Path.Combine(
                    OutputFolder,
                    Path.GetFileNameWithoutExtension(item.SourcePath) + "-comprimido.pdf");

                try
                {
                    var fileProgress = new Progress<double>(p =>
                        Progress = (done * 100d + p) / pending.Count);

                    var result = await _compressionService.CompressAsync(
                        item.SourcePath, output, mode, Strength, fileProgress);

                    item.CompressedBytes = result.CompressedBytes;
                    item.SavedPercent = result.SavedPercent;
                    item.Improved = result.Improved;
                    item.Status = ConversionStatus.Concluido;

                    totalBefore += result.OriginalBytes;
                    totalAfter += result.CompressedBytes;
                }
                catch (Exception ex)
                {
                    item.Status = ConversionStatus.Erro;
                    item.ErrorMessage = ex.Message;
                    failed++;
                }

                done++;
                Progress = done * 100d / pending.Count;
            }

            var saved = totalBefore > 0 ? (totalBefore - totalAfter) * 100d / totalBefore : 0;

            StatusMessage = failed == 0
                ? $"Concluido: {saved:0}% menor no total, em {OutputFolder}"
                : $"{done - failed} comprimido(s), {failed} com erro. Passe o mouse sobre o item para ver o motivo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCompress() => Items.Count > 0;
}
