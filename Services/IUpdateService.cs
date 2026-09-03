namespace IHatePdf.Services;

/// <summary>Resultado de uma verificacao de atualizacao contra as releases do GitHub.</summary>
/// <param name="IsUpdateAvailable">true quando a release publicada e mais nova que a versao instalada.</param>
/// <param name="LatestVersion">Versao da ultima release (sem o "v").</param>
/// <param name="ReleaseName">Titulo da release.</param>
/// <param name="ReleaseNotes">Corpo da release em markdown, como publicado.</param>
/// <param name="ReleasePageUrl">Pagina da release no GitHub.</param>
/// <param name="DownloadUrl">Asset baixavel (.zip/.exe/.msi), quando a release tiver um.</param>
/// <param name="DownloadFileName">Nome do arquivo do asset.</param>
public sealed record UpdateInfo(
    bool IsUpdateAvailable,
    string LatestVersion,
    string? ReleaseName,
    string? ReleaseNotes,
    string? ReleasePageUrl,
    string? DownloadUrl,
    string? DownloadFileName);

/// <summary>Distribuicao por GitHub Releases: verifica, baixa e abre a pasta do pacote.</summary>
public interface IUpdateService
{
    /// <summary>Versao instalada, lida do assembly.</summary>
    string CurrentVersion { get; }

    /// <summary>URL do repositorio publico.</summary>
    string RepositoryUrl { get; }

    Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default);

    /// <summary>Baixa o asset da release e devolve o caminho local do arquivo.</summary>
    Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Baixa a release, extrai o pacote e devolve o caminho do executavel novo,
    /// pronto para substituir o atual. Nada e alterado na instalacao ainda.
    /// </summary>
    Task<string> DownloadAndStageAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Verifica se o executavel atual pode ser sobrescrito (pasta gravavel).
    /// Instalacoes em Program Files exigiriam elevacao e sao recusadas aqui.
    /// </summary>
    bool CanUpdateInPlace(out string reason);

    /// <summary>
    /// Dispara o script que espera este processo encerrar, troca o executavel
    /// e reabre o app. Chame <see cref="IApplicationService.Shutdown"/> em seguida.
    /// </summary>
    void ApplyUpdateAndRestart(string stagedExecutablePath);

    /// <summary>Abre uma URL no navegador padrao.</summary>
    void OpenInBrowser(string url);

    /// <summary>Abre o Explorer com o arquivo selecionado.</summary>
    void RevealInExplorer(string filePath);
}
