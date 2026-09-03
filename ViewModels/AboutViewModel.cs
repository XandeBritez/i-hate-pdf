using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IHatePdf.Services;

namespace IHatePdf.ViewModels;

/// <summary>Tela Sobre: identidade, o que o app faz, e atualizacao via GitHub Releases.</summary>
public sealed partial class AboutViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;
    private readonly IDialogService _dialogService;

    private UpdateInfo? _lastCheck;

    public AboutViewModel(IUpdateService updateService, IDialogService dialogService)
        : base("Sobre", "")
    {
        _updateService = updateService;
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

    [RelayCommand(CanExecute = nameof(HasUpdate))]
    private async Task DownloadUpdateAsync()
    {
        if (_lastCheck is null) return;

        // Release sem pacote anexado: manda o usuario para a pagina da release.
        if (string.IsNullOrWhiteSpace(_lastCheck.DownloadUrl))
        {
            if (_lastCheck.ReleasePageUrl is not null)
                _updateService.OpenInBrowser(_lastCheck.ReleasePageUrl);
            return;
        }

        try
        {
            IsDownloading = true;
            DownloadProgress = 0;
            UpdateStatus = $"Baixando {_lastCheck.DownloadFileName}...";

            var progress = new Progress<double>(p => DownloadProgress = p);
            var file = await _updateService.DownloadAsync(_lastCheck, progress);

            UpdateStatus = $"Baixado em {file}";
            _updateService.RevealInExplorer(file);
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Falha no download: {ex.Message}";
            _dialogService.ShowError("Erro ao baixar", ex.Message);
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
