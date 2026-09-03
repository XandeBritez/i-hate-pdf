using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHatePdf.Models;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>
/// Editor visual de paginas: miniaturas em grade, selecao multipla, exclusao,
/// insercao de paginas de outros PDFs e reordenacao por arrastar.
/// O estado e apenas a lista ordenada de <see cref="PageItem"/>; salvar
/// reconstroi o documento a partir dela.
/// </summary>
public sealed partial class EditorViewModel : ViewModelBase
{
    private const string PdfFilter = "Documentos PDF (*.pdf)|*.pdf";
    private const int ThumbnailWidth = 220;

    private readonly IPdfService _pdfService;
    private readonly IPdfRenderService _renderService;
    private readonly IDialogService _dialogService;

    private CancellationTokenSource? _thumbnailCts;

    public EditorViewModel(IPdfService pdfService, IPdfRenderService renderService, IDialogService dialogService)
        : base("Editar paginas", "")
    {
        _pdfService = pdfService;
        _renderService = renderService;
        _dialogService = dialogService;

        Pages.CollectionChanged += OnPagesChanged;
    }

    public ObservableCollection<PageItem> Pages { get; } = new();

    [ObservableProperty] private string? _currentFilePath;

    public string CurrentFileName =>
        CurrentFilePath is null ? "Nenhum documento aberto" : Path.GetFileName(CurrentFilePath);

    partial void OnCurrentFilePathChanged(string? value) => OnPropertyChanged(nameof(CurrentFileName));

    private void OnPagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Renumera apos qualquer insercao/remocao/movimento (inclusive drag and drop).
        for (var i = 0; i < Pages.Count; i++)
            Pages[i].DisplayNumber = i + 1;

        SaveAsCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        SelectAllCommand.NotifyCanExecuteChanged();
        ClearSelectionCommand.NotifyCanExecuteChanged();
        AddPagesCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        var paths = _dialogService.OpenFiles("Abrir PDF", PdfFilter, multiselect: false);
        if (paths is { Length: > 0 })
            await LoadDocumentAsync(paths[0]);
    }

    public override async Task AcceptFilesAsync(IReadOnlyList<string> paths)
    {
        var pdfs = paths
            .Where(p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pdfs.Count == 0)
        {
            StatusMessage = "O editor aceita apenas arquivos PDF.";
            return;
        }

        // Sem documento aberto: o primeiro PDF vira o documento; os demais sao anexados.
        if (CurrentFilePath is null)
        {
            await LoadDocumentAsync(pdfs[0]);
            pdfs.RemoveAt(0);
        }

        if (pdfs.Count > 0)
            await AppendPagesAsync(pdfs);
    }

    private async Task LoadDocumentAsync(string path)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Carregando miniaturas...";

            CancelThumbnails();
            Pages.Clear();
            CurrentFilePath = path;

            var references = await _pdfService.GetPagesAsync(path);
            foreach (var reference in references)
                Pages.Add(new PageItem(reference.SourcePath, reference.PageIndex));

            MarkExternalPages();
            await RenderThumbnailsAsync();
            StatusMessage = $"{Pages.Count} pagina(s) carregada(s).";
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("Erro ao abrir", ex.Message);
            StatusMessage = "Falha ao abrir o documento.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task AddPagesAsync()
    {
        var paths = _dialogService.OpenFiles("Adicionar paginas de outros PDFs", PdfFilter);
        if (paths is not null)
            await AppendPagesAsync(paths);
    }

    private async Task AppendPagesAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                foreach (var reference in await _pdfService.GetPagesAsync(path))
                    Pages.Add(new PageItem(reference.SourcePath, reference.PageIndex));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Arquivo invalido", Path.GetFileName(path) + ": " + ex.Message);
            }
        }

        MarkExternalPages();
        await RenderThumbnailsAsync();
        StatusMessage = $"{Pages.Count} pagina(s) no documento.";
    }

    /// <summary>Marca as paginas trazidas de outros arquivos; so nelas a origem e exibida.</summary>
    private void MarkExternalPages()
    {
        foreach (var page in Pages)
            page.IsFromOtherFile = !string.Equals(page.SourcePath, CurrentFilePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Renderiza somente as miniaturas ainda ausentes, fora da thread de UI.</summary>
    private async Task RenderThumbnailsAsync()
    {
        CancelThumbnails();
        _thumbnailCts = new CancellationTokenSource();
        var ct = _thumbnailCts.Token;

        var pending = Pages.Where(p => p.Thumbnail is null).ToList();

        foreach (var page in pending)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                // O bitmap volta congelado (Freeze) do servico: atribuicao direta e segura.
                page.Thumbnail = await _renderService.RenderPageAsync(page.SourcePath, page.PageIndex, ThumbnailWidth, ct);
                page.IsLoading = false;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                page.IsLoading = false; // mantem o card com o placeholder de erro
            }
        }
    }

    private void CancelThumbnails()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void DeleteSelected()
    {
        var selected = Pages.Where(p => p.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Selecione ao menos uma pagina.";
            return;
        }

        if (selected.Count == Pages.Count)
        {
            _dialogService.ShowError("Operacao invalida", "O documento precisa manter pelo menos uma pagina.");
            return;
        }

        foreach (var page in selected)
            Pages.Remove(page);

        StatusMessage = $"{selected.Count} pagina(s) removida(s).";
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void SelectAll()
    {
        foreach (var page in Pages) page.IsSelected = true;
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void ClearSelection()
    {
        foreach (var page in Pages) page.IsSelected = false;
    }

    [RelayCommand]
    private void Close()
    {
        CancelThumbnails();
        Pages.Clear();
        CurrentFilePath = null;
        StatusMessage = string.Empty;
    }

    private bool HasDocument() => Pages.Count > 0;

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task SaveAsAsync()
    {
        var suggestion = CurrentFilePath is null
            ? "documento-editado.pdf"
            : Path.GetFileNameWithoutExtension(CurrentFilePath) + "-editado.pdf";

        var output = _dialogService.SaveFile("Salvar documento", PdfFilter, suggestion);
        if (output is null) return;

        try
        {
            IsBusy = true;
            StatusMessage = "Gravando...";
            await _pdfService.BuildAsync(Pages.Select(p => p.ToReference()), output);
            _renderService.Invalidate(output);
            StatusMessage = $"PDF gerado: {output}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Falha ao gravar.";
            _dialogService.ShowError("Erro ao salvar", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
