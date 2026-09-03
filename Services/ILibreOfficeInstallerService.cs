namespace IHatePdf.Services;

/// <summary>Instalador do LibreOffice localizado para download dentro do app.</summary>
/// <param name="Version">Versao estavel encontrada, ex.: "25.8.2".</param>
/// <param name="Url">URL direta do .msi para Windows x64.</param>
/// <param name="FileName">Nome do arquivo a gravar.</param>
public sealed record LibreOfficeInstaller(string Version, string Url, string FileName);

/// <summary>
/// Baixa o instalador do LibreOffice sem tirar o usuario do app.
/// O app nao instala nada sozinho: ele entrega o instalador e o usuario decide.
/// </summary>
public interface ILibreOfficeInstallerService
{
    /// <summary>Descobre a versao estavel atual no servidor da The Document Foundation.</summary>
    Task<LibreOfficeInstaller> FindLatestAsync(CancellationToken ct = default);

    /// <summary>Baixa o .msi para a pasta Downloads e devolve o caminho local.</summary>
    Task<string> DownloadAsync(LibreOfficeInstaller installer, IProgress<double>? progress = null, CancellationToken ct = default);

    /// <summary>Abre o instalador baixado (o Windows pedira elevacao).</summary>
    void RunInstaller(string installerPath);
}
