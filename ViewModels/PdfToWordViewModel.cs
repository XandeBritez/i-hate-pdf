using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHatePdf.Models;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>Caminho inverso: fila de PDFs virando Word ou texto puro.</summary>
public sealed partial class PdfToWordViewModel : ViewModelBase
{
    private const string PdfFilter = "Documentos PDF (*.pdf)|*.pdf";

    private readonly IPdfExportService _exportService;
    private readonly ILibreOfficeInstallerService _installerService;
    private readonly IDialogService _dialogService;

    public PdfToWordViewModel(
        IPdfExportService exportService,
        ILibreOfficeInstallerService installerService,
        IDialogService dialogService)
        : base("PDF para Word", "")
    {
        _exportService = exportService;
        _installerService = installerService;
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

    /// <summary>Word por padrao; texto puro para quem so quer o conteudo.</summary>
    [ObservableProperty] private bool _exportAsWord = true;

    /// <summary>Sem LibreOffice esta tela nao funciona; a UI avisa antes da tentativa.</summary>
    public bool IsLibreOfficeMissing => !_exportService.IsAvailable;

    // ===== Instalacao do LibreOffice sem sair do app =====

    [ObservableProperty] private bool _isDownloadingLibreOffice;
    [ObservableProperty] private double _libreOfficeProgress;
    [ObservableProperty] private string _libreOfficeStatus = string.Empty;

    /// <summary>Baixa o instalador e oferece abri-lo; instalar continua sendo decisao do usuario.</summary>
    [RelayCommand]
    private async Task InstallLibreOfficeAsync()
    {
        try
        {
            IsDownloadingLibreOffice = true;
            LibreOfficeProgress = 0;
            LibreOfficeStatus = "Procurando a versao atual...";

            var installer = await _installerService.FindLatestAsync();
            LibreOfficeStatus = $"Baixando LibreOffice {installer.Version} (cerca de 350 MB)...";

            var progress = new Progress<double>(p => LibreOfficeProgress = p);
            var file = await _installerService.DownloadAsync(installer, progress);

            LibreOfficeStatus = $"Instalador salvo em {file}";

            if (_dialogService.Confirm(
                    "Instalador baixado",
                    $"LibreOffice {installer.Version} foi baixado.{Environment.NewLine}{Environment.NewLine}" +
                    "Abrir o instalador agora? O Windows vai pedir permissao de administrador."))
            {
                _installerService.RunInstaller(file);
                LibreOfficeStatus = "Instalador aberto. Quando terminar, use \"Ja instalei\".";
            }
        }
        catch (Exception ex)
        {
            LibreOfficeStatus = $"Falha ao baixar: {ex.Message}";
            _dialogService.ShowError("Erro ao baixar o LibreOffice", ex.Message);
        }
        finally
        {
            IsDownloadingLibreOffice = false;
        }
    }

    /// <summary>Reavalia a presenca do LibreOffice depois de uma instalacao.</summary>
    [RelayCommand]
    private void RecheckLibreOffice()
    {
        OnPropertyChanged(nameof(IsLibreOfficeMissing));

        LibreOfficeStatus = _exportService.IsAvailable
            ? "LibreOffice encontrado. A conversao esta liberada."
            : "Ainda nao encontrei o LibreOffice. Se acabou de instalar, conclua a instalacao e tente de novo.";
    }

    private PdfExportFormat Format => ExportAsWord ? PdfExportFormat.Word : PdfExportFormat.PlainText;

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var paths = _dialogService.OpenFiles("Selecione os PDFs", PdfFilter);
        if (paths is not null)
            await AcceptFilesAsync(paths);
    }

    public override Task AcceptFilesAsync(IReadOnlyList<string> paths)
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

            Items.Add(new ConversionItem(path));
        }

        StatusMessage = ignored > 0
            ? $"{Items.Count} PDF(s) na fila. {ignored} ignorado(s): esta tela aceita apenas PDF."
            : $"{Items.Count} PDF(s) na fila.";

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

            var extension = _exportService.GetExtension(Format);
            var ok = 0;
            var failed = 0;

            foreach (var item in Items.Where(i => i.Status != ConversionStatus.Concluido))
            {
                item.Status = ConversionStatus.Convertendo;
                item.ErrorMessage = null;
                StatusMessage = $"Convertendo {item.FileName}...";

                var output = Path.Combine(
                    OutputFolder,
                    Path.GetFileNameWithoutExtension(item.SourcePath) + extension);

                try
                {
                    item.OutputPath = await _exportService.ExportAsync(item.SourcePath, output, Format);
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
                ? $"{ok} arquivo(s) gerado(s) em {OutputFolder}"
                : $"{ok} convertido(s), {failed} com erro. Passe o mouse sobre o item para ver o motivo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanConvert() => Items.Count > 0;
}
