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

    /// <summary>Abre uma URL no navegador padrao.</summary>
    void OpenInBrowser(string url);

    /// <summary>Abre o Explorer com o arquivo selecionado.</summary>
    void RevealInExplorer(string filePath);
}
