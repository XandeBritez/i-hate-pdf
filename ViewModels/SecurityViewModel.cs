using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHatePdf.Models;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>Coloca e tira a senha de abertura dos PDFs da fila.</summary>
public sealed partial class SecurityViewModel : ViewModelBase
{
    private const string PdfFilter = "Documentos PDF (*.pdf)|*.pdf";

    private readonly IPdfSecurityService _securityService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settings;

    public SecurityViewModel(
        IPdfSecurityService securityService,
        IDialogService dialogService,
        ISettingsService settings)
        : base("Senha", "")
    {
        _securityService = securityService;
        _dialogService = dialogService;
        _settings = settings;

        OutputFolder = settings.Current.SecurityOutputFolder ?? AppSettings.DefaultOutputFolder;

        Items.CollectionChanged += (_, _) => ApplyCommand.NotifyCanExecuteChanged();
    }

    public ObservableCollection<ConversionItem> Items { get; } = new();

    [ObservableProperty] private string _outputFolder = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    private ConversionItem? _selectedItem;

    /// <summary>true = colocar senha; false = tirar.</summary>
    [ObservableProperty] private bool _protectMode = true;

    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _passwordConfirmation = string.Empty;

    partial void OnProtectModeChanged(bool value)
    {
        Password = string.Empty;
        PasswordConfirmation = string.Empty;
        OnPropertyChanged(nameof(ActionLabel));
    }

    partial void OnOutputFolderChanged(string value)
    {
        _settings.Current.SecurityOutputFolder = value;
        _settings.Save();
    }

    public string ActionLabel => ProtectMode ? "Proteger" : "Remover senha";

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

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        if (string.IsNullOrEmpty(Password))
        {
            StatusMessage = ProtectMode
                ? "Digite a senha que devera abrir os arquivos."
                : "Digite a senha atual dos arquivos.";
            return;
        }

        // Errar a senha ao proteger deixaria o arquivo inacessivel para sempre:
        // a confirmacao existe so nesse sentido.
        if (ProtectMode && Password != PasswordConfirmation)
        {
            StatusMessage = "As senhas nao conferem.";
            return;
        }

        try
        {
            IsBusy = true;
            Directory.CreateDirectory(OutputFolder);

            var suffix = ProtectMode ? "-protegido" : "-sem-senha";
            var ok = 0;
            var failed = 0;

            foreach (var item in Items.Where(i => i.Status != ConversionStatus.Concluido))
            {
                item.Status = ConversionStatus.Convertendo;
                item.ErrorMessage = null;
                StatusMessage = $"Processando {item.FileName}...";

                var output = Path.Combine(
                    OutputFolder,
                    Path.GetFileNameWithoutExtension(item.SourcePath) + suffix + ".pdf");

                try
                {
                    if (ProtectMode)
                        await _securityService.ProtectAsync(item.SourcePath, output, Password);
                    else
                        await _securityService.RemoveAsync(item.SourcePath, output, Password);

                    item.OutputPath = output;
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
                ? $"{ok} arquivo(s) em {OutputFolder}"
                : $"{ok} concluido(s), {failed} com erro. Passe o mouse sobre o item para ver o motivo.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanApply() => Items.Count > 0;
}
