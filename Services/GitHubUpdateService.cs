using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace IHatePdf.Services;

/// <summary>
/// Verificacao e download de novas versoes pela API publica de releases do GitHub.
/// Sem token: a API anonima basta para ler releases de um repositorio publico.
/// </summary>
public sealed class GitHubUpdateService : IUpdateService
{
    private const string Owner = "XandeBritez";
    private const string Repo = "i-hate-pdf";

    private static readonly string[] AssetExtensions = [".zip", ".exe", ".msi"];

    private readonly HttpClient _http;

    public GitHubUpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // A API do GitHub rejeita requisicoes sem User-Agent.
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("IHatePdf", CurrentVersion));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public string CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetName().Version is { } v
            ? $"{v.Major}.{v.Minor}.{v.Build}"
            : "1.0.0";

    public string RepositoryUrl => $"https://github.com/{Owner}/{Repo}";

    public async Task<UpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new InvalidOperationException("Nenhuma release publicada ainda neste repositorio.");

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = json.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        var latest = tag.TrimStart('v', 'V');

        string? downloadUrl = null, downloadName = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                if (!AssetExtensions.Any(ext => name.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    continue;

                downloadName = name;
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        return new UpdateInfo(
            IsUpdateAvailable: IsNewer(latest, CurrentVersion),
            LatestVersion: latest,
            ReleaseName: root.TryGetProperty("name", out var n) ? n.GetString() : null,
            ReleaseNotes: root.TryGetProperty("body", out var b) ? b.GetString() : null,
            ReleasePageUrl: root.TryGetProperty("html_url", out var h) ? h.GetString() : null,
            DownloadUrl: downloadUrl,
            DownloadFileName: downloadName);
    }

    /// <summary>Compara versoes numericamente; formatos invalidos nunca disparam atualizacao.</summary>
    private static bool IsNewer(string candidate, string current) =>
        Version.TryParse(Normalize(candidate), out var a) &&
        Version.TryParse(Normalize(current), out var b) &&
        a > b;

    private static string Normalize(string version)
    {
        // "1.2" -> "1.2.0"; descarta sufixos como "-beta".
        var core = version.Split('-', '+')[0].Trim();
        var parts = core.Split('.').Length;
        return parts switch { 1 => core + ".0.0", 2 => core + ".0", _ => core };
    }

    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
            throw new InvalidOperationException("Esta release nao publicou um arquivo para download.");

        var target = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            update.DownloadFileName ?? $"IHatePdf-{update.LatestVersion}.zip");

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        using var response = await _http
            .GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var destination = File.Create(target);

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

        return target;
    }

    public async Task<string> DownloadAndStageAsync(
        UpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadUrl))
            throw new InvalidOperationException("Esta release nao publicou um pacote para atualizacao automatica.");

        var stageDir = Path.Combine(Path.GetTempPath(), "IHatePdf", "update-" + update.LatestVersion);
        if (Directory.Exists(stageDir))
            Directory.Delete(stageDir, recursive: true);
        Directory.CreateDirectory(stageDir);

        var package = Path.Combine(stageDir, update.DownloadFileName ?? "pacote.zip");

        using (var response = await _http
                   .GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                   .ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var destination = File.Create(package);

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

        // Release pode publicar o .exe direto ou dentro de um .zip.
        if (package.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return package;

        var extracted = Path.Combine(stageDir, "extraido");
        Directory.CreateDirectory(extracted);
        ZipFile.ExtractToDirectory(package, extracted, overwriteFiles: true);

        var exe = Directory
            .EnumerateFiles(extracted, "*.exe", SearchOption.AllDirectories)
            .OrderByDescending(f => Path.GetFileName(f).Equals("IHatePdf.exe", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault()
            ?? throw new InvalidOperationException("O pacote da release nao contem um executavel.");

        return exe;
    }

    public bool CanUpdateInPlace(out string reason)
    {
        var current = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(current))
        {
            reason = "Nao foi possivel identificar o executavel em execucao.";
            return false;
        }

        var folder = Path.GetDirectoryName(current)!;

        // Teste real de escrita: permissao efetiva depende de ACL, nao do caminho.
        try
        {
            var probe = Path.Combine(folder, $".ihatepdf-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
        }
        catch (Exception)
        {
            reason =
                $"A pasta '{folder}' e somente leitura para o seu usuario. " +
                "Mova o app para uma pasta pessoal ou baixe a nova versao manualmente.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public void ApplyUpdateAndRestart(string stagedExecutablePath)
    {
        var current = Environment.ProcessPath
            ?? throw new InvalidOperationException("Nao foi possivel identificar o executavel em execucao.");

        var script = Path.Combine(Path.GetTempPath(), "IHatePdf", $"update-{Guid.NewGuid():N}.cmd");
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);

        // O executavel em uso nao pode se sobrescrever: um script externo faz a troca.
        //
        // A espera nao usa tasklist: enquanto o app estiver aberto o proprio
        // "move" falha, entao tentar mover ja e o teste de "o processo saiu?".
        // Isso evita depender de tasklist/find/ping, que sao executaveis do
        // System32 e somem se o PATH estiver quebrado.
        // O timeout e chamado por caminho absoluto pelo mesmo motivo.
        var timeout = Path.Combine(Environment.SystemDirectory, "timeout.exe");

        var cmd = $"""
            @echo off
            setlocal
            set "TARGET={current}"
            set "SOURCE={stagedExecutablePath}"
            set "BACKUP={current}.old"

            if exist "%BACKUP%" del /f /q "%BACKUP%" >nul 2>&1

            rem Enquanto o app estiver rodando, o arquivo esta bloqueado e o move falha.
            for /l %%i in (1,1,60) do (
                move /y "%TARGET%" "%BACKUP%" >nul 2>&1 && goto :copiar
                "{timeout}" /t 1 /nobreak >nul 2>&1
            )
            goto :fim

            :copiar
            copy /y "%SOURCE%" "%TARGET%" >nul 2>&1
            if errorlevel 1 (
                rem Copia falhou: devolve a versao anterior para nao deixar o usuario sem app.
                move /y "%BACKUP%" "%TARGET%" >nul 2>&1
            ) else (
                del /f /q "%BACKUP%" >nul 2>&1
            )

            start "" "%TARGET%"

            :fim
            del /f /q "%~f0" >nul 2>&1
            """;

        File.WriteAllText(script, cmd, Encoding.Default);

        // cmd.exe por caminho absoluto: nao depende do PATH do processo.
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            Arguments = $"/c \"\"{script}\"\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    public void OpenInBrowser(string url) =>
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

    public void RevealInExplorer(string filePath) =>
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{filePath}\"",
            UseShellExecute = true
        });
}
