using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHatePdf.Models;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>Fila de PDFs a unificar. A ordem da colecao e a ordem do arquivo final.</summary>
public sealed partial class MergeViewModel : ViewModelBase
{
    private const string PdfFilter = "Documentos PDF (*.pdf)|*.pdf";

    private const int ThumbnailWidth = 220;

    private readonly IPdfService _pdfService;
    private readonly IPdfRenderService _renderService;
    private readonly IDialogService _dialogService;

    private CancellationTokenSource? _thumbnailCts;

    public MergeViewModel(IPdfService pdfService, IPdfRenderService renderService, IDialogService dialogService)
        : base("Unir PDFs", "")
    {
        _pdfService = pdfService;
        _renderService = renderService;
        _dialogService = dialogService;
        Files.CollectionChanged += OnFilesChanged;
    }

    private void OnFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Renumera apos qualquer insercao/remocao/movimento (inclusive drag and drop).
        for (var i = 0; i < Files.Count; i++)
            Files[i].DisplayNumber = i + 1;

        MergeCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<PdfFileItem> Files { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDownCommand))]
    private PdfFileItem? _selectedFile;

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var paths = _dialogService.OpenFiles("Selecione os PDFs", PdfFilter);
        if (paths is not null)
            await AcceptFilesAsync(paths);
    }

    public override async Task AcceptFilesAsync(IReadOnlyList<string> paths)
    {
        foreach (var path in paths.Where(IsPdf))
        {
            try
            {
                var item = new PdfFileItem
                {
                    FilePath = path,
                    SizeInBytes = new FileInfo(path).Length,
                    PageCount = await _pdfService.GetPageCountAsync(path)
                };
                Files.Add(item);
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Arquivo invalido", $"Nao foi possivel ler '{Path.GetFileName(path)}': {ex.Message}");
            }
        }

        await RenderThumbnailsAsync();

        StatusMessage = $"{Files.Count} arquivo(s) na fila.";
    }

    /// <summary>Renderiza a capa (primeira pagina) dos arquivos ainda sem miniatura.</summary>
    private async Task RenderThumbnailsAsync()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = new CancellationTokenSource();
        var ct = _thumbnailCts.Token;

        foreach (var file in Files.Where(f => f.Thumbnail is null).ToList())
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                // O bitmap volta congelado (Freeze) do servico: atribuicao direta e segura.
                file.Thumbnail = await _renderService.RenderPageAsync(file.FilePath, 0, ThumbnailWidth, ct);
                file.IsLoading = false;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                file.IsLoading = false; // card fica sem miniatura, mas o arquivo continua na fila
            }
        }
    }

    private static bool IsPdf(string path) =>
        string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        if (SelectedFile is not null)
            Files.Remove(SelectedFile);
    }

    [RelayCommand]
    private void Clear()
    {
        Files.Clear();
        StatusMessage = string.Empty;
    }

    private bool HasSelection() => SelectedFile is not null;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        var index = Files.IndexOf(SelectedFile!);
        Files.Move(index, index - 1);
    }

    private bool CanMoveUp() => SelectedFile is not null && Files.IndexOf(SelectedFile) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        var index = Files.IndexOf(SelectedFile!);
        Files.Move(index, index + 1);
    }

    private bool CanMoveDown() => SelectedFile is not null && Files.IndexOf(SelectedFile) < Files.Count - 1;

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private async Task MergeAsync()
    {
        var output = _dialogService.SaveFile("Salvar PDF unificado", PdfFilter, "documento-unificado.pdf");
        if (output is null) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Unificando...";
            await _pdfService.MergeAsync(Files.Select(f => f.FilePath), output);
            StatusMessage = $"PDF gerado: {output}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Falha ao unificar.";
            _dialogService.ShowError("Erro ao unificar", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanMerge() => Files.Count >= 2;
}
