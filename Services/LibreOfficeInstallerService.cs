using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace IHatePdf.Services;

/// <summary>
/// Descobre e baixa o instalador estavel do LibreOffice a partir do mirror
/// oficial da The Document Foundation.
///
/// A versao nao e fixada no codigo: o diretorio /stable/ e lido e a maior
/// versao encontrada e usada, senao o link envelheceria a cada release deles.
/// </summary>
public sealed partial class LibreOfficeInstallerService : ILibreOfficeInstallerService
{
    private const string StableIndex = "https://download.documentfoundation.org/libreoffice/stable/";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    [GeneratedRegex(@"href=""(\d+\.\d+\.\d+)/""", RegexOptions.IgnoreCase)]
    private static partial Regex VersionLink();

    public async Task<LibreOfficeInstaller> FindLatestAsync(CancellationToken ct = default)
    {
        var index = await _http.GetStringAsync(StableIndex, ct).ConfigureAwait(false);

        var version = VersionLink().Matches(index)
            .Select(m => m.Groups[1].Value)
            .Select(v => (Text: v, Parsed: Version.TryParse(v, out var p) ? p : null))
            .Where(v => v.Parsed is not null)
            .OrderByDescending(v => v.Parsed)
            .Select(v => v.Text)
            .FirstOrDefault()
            ?? throw new ConversionException(
                "Nao foi possivel descobrir a versao atual do LibreOffice. " +
                "Baixe manualmente em libreoffice.org.");

        var fileName = $"LibreOffice_{version}_Win_x86-64.msi";
        var url = $"{StableIndex}{version}/win/x86_64/{fileName}";

        return new LibreOfficeInstaller(version, url, fileName);
    }

    public async Task<string> DownloadAsync(
        LibreOfficeInstaller installer,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            installer.FileName);

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        using var response = await _http
            .GetAsync(installer.Url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        // Grava em .part e so renomeia no fim: um download interrompido nunca
        // vira um .msi truncado que o usuario tentaria executar.
        var partial = target + ".part";
        await using (var destination = File.Create(partial))
        {
            var buffer = new byte[81920];
            long received = 0;
            int read;

            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                received += read;

                if (total is > 0)
                    progress?.Report(received * 100d / total.Value);
            }
        }

        File.Move(partial, target, overwrite: true);
        return target;
    }

    public void RunInstaller(string installerPath) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true   // dispara o UAC; a instalacao e decisao do usuario
        });
}
