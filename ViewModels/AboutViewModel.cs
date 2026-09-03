using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>Tela Sobre: identidade, o que o app faz, e atualizacao via GitHub Releases.</summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;
    private readonly IApplicationService _applicationService;
    private readonly IDialogService _dialogService;

    private UpdateInfo? _lastCheck;

    public AboutViewModel(
        IUpdateService updateService,
        IApplicationService applicationService,
        IDialogService dialogService)
        : base("Sobre", "")
    {
        _updateService = updateService;
        _applicationService = applicationService;
        _dialogService = dialogService;
    }

    public string Version => _updateService.CurrentVersion;
    public string RepositoryUrl => _updateService.RepositoryUrl;

    /// <summary>Resultado da ultima verificacao, em uma frase.</summary>
    [ObservableProperty] private string _updateStatus = "Nunca verificado.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadUpdateCommand))]
    private bool _hasUpdate;

    [ObservableProperty] private string? _latestVersion;

    [ObservableProperty] private double _downloadProgress;

    [ObservableProperty] private bool _isDownloading;

    public override Task AcceptFilesAsync(IReadOnlyList<string> paths)
    {
        StatusMessage = "Solte arquivos nas telas de unir, editar ou converter.";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            IsBusy = true;
            UpdateStatus = "Consultando o GitHub...";

            _lastCheck = await _updateService.CheckForUpdatesAsync();
            LatestVersion = _lastCheck.LatestVersion;
            HasUpdate = _lastCheck.IsUpdateAvailable;

            UpdateStatus = HasUpdate
                ? $"Versao {_lastCheck.LatestVersion} disponivel. Voce esta na {Version}."
                : $"Voce esta na versao mais recente ({Version}).";
        }
        catch (Exception ex)
        {
            HasUpdate = false;
            UpdateStatus = $"Nao deu para verificar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Atualizacao completa sem sair do app: baixa o pacote, extrai o
    /// executavel novo e dispara a troca com reinicio automatico.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasUpdate))]
    private async Task DownloadUpdateAsync()
    {
        if (_lastCheck is null) return;

        // Release sem pacote anexado: nao ha o que instalar automaticamente.
        if (string.IsNullOrWhiteSpace(_lastCheck.DownloadUrl))
        {
            if (_lastCheck.ReleasePageUrl is not null)
                _updateService.OpenInBrowser(_lastCheck.ReleasePageUrl);
            return;
        }

        // Pasta somente leitura (ex.: Program Files) exigiria elevacao:
        // avisa e oferece o download manual em vez de falhar no meio da troca.
        if (!_updateService.CanUpdateInPlace(out var reason))
        {
            UpdateStatus = reason;
            if (_dialogService.Confirm("Atualizacao automatica indisponivel", reason + " Abrir a pagina da release?")
                && _lastCheck.ReleasePageUrl is not null)
            {
                _updateService.OpenInBrowser(_lastCheck.ReleasePageUrl);
            }
            return;
        }

        if (!_dialogService.Confirm(
                "Atualizar agora",
                $"O app vai baixar a versao {_lastCheck.LatestVersion}, fechar e reabrir sozinho.{Environment.NewLine}{Environment.NewLine}" +
                "Salve o que estiver em andamento antes de continuar."))
        {
            return;
        }

        try
        {
            IsDownloading = true;
            DownloadProgress = 0;
            UpdateStatus = $"Baixando a versao {_lastCheck.LatestVersion}...";

            var progress = new Progress<double>(p => DownloadProgress = p);
            var staged = await _updateService.DownloadAndStageAsync(_lastCheck, progress);

            UpdateStatus = "Aplicando a atualizacao. O app vai reabrir em instantes...";

            _updateService.ApplyUpdateAndRestart(staged);
            _applicationService.Shutdown();
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Falha ao atualizar: {ex.Message}";
            _dialogService.ShowError("Erro ao atualizar", ex.Message);
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void OpenRepository() => _updateService.OpenInBrowser(RepositoryUrl);

    [RelayCommand]
    private void OpenReleases() => _updateService.OpenInBrowser($"{RepositoryUrl}/releases");

    [RelayCommand]
    private void OpenIssues() => _updateService.OpenInBrowser($"{RepositoryUrl}/issues");
}
