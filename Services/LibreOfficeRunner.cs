using System.Diagnostics;
using Microsoft.Win32;

namespace IHatePdf.Services;

/// <summary>
/// Executa o LibreOffice em modo headless. Compartilhado pelos conversores
/// (Office -> PDF e PDF -> Word), que so mudam o filtro de saida/entrada.
/// </summary>
public static class LibreOfficeRunner
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Converte um arquivo e copia o resultado para <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="convertTo">Alvo do --convert-to, ex.: "pdf" ou "docx:MS Word 2007 XML".</param>
    /// <param name="outputExtension">Extensao que o soffice dara ao arquivo gerado, sem ponto.</param>
    /// <param name="inFilter">Filtro de importacao (--infilter), ex.: "writer_pdf_import".</param>
    public static async Task ConvertAsync(
        string inputPath,
        string outputPath,
        string convertTo,
        string outputExtension,
        string? inFilter,
        CancellationToken ct)
    {
        var soffice = FindSoffice()
            ?? throw new ConversionException(
                "LibreOffice nao encontrado. Instale o LibreOffice (https://www.libreoffice.org) " +
                "para converter estes formatos.");

        // Diretorio de saida proprio: o soffice nomeia o arquivo como <origem>.<ext>.
        var workDir = Path.Combine(Path.GetTempPath(), "IHatePdf", "conv-" + Guid.NewGuid().ToString("N"));
        // Perfil de usuario dedicado: sem isso execucoes concorrentes/repetidas travam no lock do perfil.
        var profileDir = Path.Combine(workDir, "profile");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(profileDir);

        try
        {
            var psi = new ProcessStartInfo(soffice)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workDir
            };
            psi.ArgumentList.Add($"-env:UserInstallation={new Uri(profileDir).AbsoluteUri}");
            psi.ArgumentList.Add("--headless");
            psi.ArgumentList.Add("--norestore");
            psi.ArgumentList.Add("--nolockcheck");
            psi.ArgumentList.Add("--nodefault");
            psi.ArgumentList.Add("--nofirststartwizard");

            if (!string.IsNullOrWhiteSpace(inFilter))
                psi.ArgumentList.Add($"--infilter={inFilter}");

            psi.ArgumentList.Add("--convert-to");
            psi.ArgumentList.Add(convertTo);
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
                try { process.Kill(entireProcessTree: true); } catch { /* ja encerrou */ }
                throw new ConversionException("A conversao excedeu o tempo limite do LibreOffice.");
            }

            // soffice retorna 0 mesmo em algumas falhas: valide pelo arquivo gerado.
            var produced = Path.Combine(workDir,
                Path.GetFileNameWithoutExtension(inputPath) + "." + outputExtension);

            if (!File.Exists(produced))
            {
                var log = ((await stdout.ConfigureAwait(false)) + Environment.NewLine +
                           (await stderr.ConfigureAwait(false))).Trim();
                throw new ConversionException(
                    $"O LibreOffice nao gerou a saida de '{Path.GetFileName(inputPath)}'. " +
                    (string.IsNullOrWhiteSpace(log) ? "Sem detalhes." : log));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.Copy(produced, outputPath, overwrite: true);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* limpeza best-effort */ }
        }
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

    public static bool IsInstalled => FindSoffice() is not null;
}
