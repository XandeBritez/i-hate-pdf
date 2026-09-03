using System.Diagnostics;
using Microsoft.Win32;

namespace IHatePdf.Services;

/// <summary>
/// DOCX/XLSX -> PDF via LibreOffice headless (soffice.exe --convert-to pdf).
/// Nao exige licenca comercial. Para trocar por Syncfusion basta registrar
/// outro IFileConverter para as mesmas extensoes.
/// </summary>
public sealed class LibreOfficeConverter : IFileConverter
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    public IReadOnlyCollection<string> Extensions { get; } =
        new[] { ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".odt", ".ods", ".rtf" };

    public async Task ConvertAsync(string inputPath, string outputPath, CancellationToken ct)
    {
        var soffice = FindSoffice()
            ?? throw new ConversionException(
                "LibreOffice nao encontrado. Instale o LibreOffice (https://www.libreoffice.org) " +
                "ou registre outro IFileConverter (ex.: Syncfusion) para DOCX/XLSX.");

        // Diretorio de saida proprio: o soffice nomeia o arquivo como <origem>.pdf.
        var workDir = Path.Combine(Path.GetTempPath(), "IHatePdf", "conv-" + Guid.NewGuid().ToString("N"));
        // Perfil de usuario dedicado: sem isso execucoes concorrentes/repetidas travam no lock do perfil.
        var profileDir = Path.Combine(workDir, "profile");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(profileDir);

        try
        {
            var profileUri = new Uri(profileDir).AbsoluteUri;

            var psi = new ProcessStartInfo(soffice)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workDir
            };
            psi.ArgumentList.Add($"-env:UserInstallation={profileUri}");
            psi.ArgumentList.Add("--headless");
            psi.ArgumentList.Add("--norestore");
            psi.ArgumentList.Add("--nolockcheck");
            psi.ArgumentList.Add("--nodefault");
            psi.ArgumentList.Add("--nofirststartwizard");
            psi.ArgumentList.Add("--convert-to");
            psi.ArgumentList.Add("pdf");
            psi.ArgumentList.Add("--outdir");
            psi.ArgumentList.Add(workDir);
            psi.ArgumentList.Add(inputPath);

            using var process = Process.Start(psi)
                ?? throw new ConversionException("Falha ao iniciar o LibreOffice.");

            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);

            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                TryKill(process);
                throw new ConversionException("A conversao excedeu o tempo limite do LibreOffice.");
            }

            // soffice retorna 0 mesmo em algumas falhas: valide pelo arquivo gerado.
            var produced = Path.Combine(workDir, Path.GetFileNameWithoutExtension(inputPath) + ".pdf");
            if (!File.Exists(produced))
            {
                var log = ((await stdout.ConfigureAwait(false)) + Environment.NewLine +
                           (await stderr.ConfigureAwait(false))).Trim();
                throw new ConversionException(
                    $"O LibreOffice nao gerou o PDF de '{Path.GetFileName(inputPath)}'. " +
                    (string.IsNullOrWhiteSpace(log) ? "Sem detalhes." : log));
            }

            File.Copy(produced, outputPath, overwrite: true);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* limpeza best-effort */ }
        }
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); } catch { /* ja encerrou */ }
    }

    /// <summary>Localiza soffice.exe pelo registro, pelos caminhos padrao e por ultimo no PATH.</summary>
    public static string? FindSoffice()
    {
        foreach (var key in new[]
                 {
                     @"SOFTWARE\LibreOffice\UNO\InstallPath",
                     @"SOFTWARE\WOW6432Node\LibreOffice\UNO\InstallPath"
                 })
        {
            var installPath = Registry.LocalMachine.OpenSubKey(key)?.GetValue(null) as string;
            if (!string.IsNullOrWhiteSpace(installPath))
            {
                var candidate = Path.Combine(installPath, "soffice.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (string.IsNullOrEmpty(root)) continue;
            var candidate = Path.Combine(root, "LibreOffice", "program", "soffice.exe");
            if (File.Exists(candidate)) return candidate;
        }

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(';'))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var candidate = Path.Combine(dir.Trim(), "soffice.exe");
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { /* entrada invalida no PATH */ }
        }

        return null;
    }
}
