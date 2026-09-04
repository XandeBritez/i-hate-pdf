using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Security;

namespace IHatePdf.Services;

/// <summary>Senha de abertura de PDF: colocar e tirar.</summary>
public interface IPdfSecurityService
{
    /// <summary>true quando o arquivo exige senha para abrir.</summary>
    Task<bool> IsProtectedAsync(string pdfPath, CancellationToken ct = default);

    /// <summary>Grava uma copia protegida por senha.</summary>
    Task ProtectAsync(string inputPath, string outputPath, string password, CancellationToken ct = default);

    /// <summary>Grava uma copia sem senha; exige a senha atual do arquivo.</summary>
    Task RemoveAsync(string inputPath, string outputPath, string currentPassword, CancellationToken ct = default);
}

/// <summary>
/// Criptografia AES do proprio PDFsharp — nao ha dependencia externa aqui.
///
/// Uma limitacao honesta: remover a senha exige saber a senha. O app nao
/// quebra protecao; ele so regrava, para quem tem a senha, um arquivo sem ela.
/// </summary>
public sealed class PdfSecurityService : IPdfSecurityService
{
    public Task<bool> IsProtectedAsync(string pdfPath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            try
            {
                using var document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
                return false;
            }
            catch (PdfReaderException)
            {
                // O PDFsharp so lanca isso quando precisa de senha para seguir.
                return true;
            }
            catch (Exception)
            {
                // Arquivo quebrado nao e arquivo protegido.
                return false;
            }
        }, ct);

    public Task ProtectAsync(string inputPath, string outputPath, string password, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            if (string.IsNullOrEmpty(password))
                throw new ConversionException("Informe a senha que devera abrir o arquivo.");

            using var document = Open(inputPath, currentPassword: null);

            // A mesma senha vai para usuario e dono: sem senha de dono, qualquer
            // leitor removeria as restricoes sozinho.
            document.SecuritySettings.UserPassword = password;
            document.SecuritySettings.OwnerPassword = password;

            // V5 = AES-256, o esquema atual do formato.
            document.SecurityHandler.SetEncryption(PdfDefaultEncryption.V5);

            Save(document, inputPath, outputPath);
        }, ct);

    public Task RemoveAsync(string inputPath, string outputPath, string currentPassword, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            if (string.IsNullOrEmpty(currentPassword))
                throw new ConversionException("Informe a senha atual do arquivo.");

            using var document = Open(inputPath, currentPassword);

            // Abrir com a senha certa da acesso de dono; daqui o documento e
            // regravado sem criptografia nenhuma.
            document.SecurityHandler.SetEncryptionToNoneAndResetPasswords();

            Save(document, inputPath, outputPath);
        }, ct);

    private static PdfDocument Open(string path, string? currentPassword)
    {
        if (!File.Exists(path))
            throw new ConversionException($"Arquivo nao encontrado: {path}");

        try
        {
            return currentPassword is null
                ? PdfReader.Open(path, PdfDocumentOpenMode.Modify)
                : PdfReader.Open(path, currentPassword, PdfDocumentOpenMode.Modify);
        }
        catch (PdfReaderException ex)
        {
            throw new ConversionException(
                currentPassword is null
                    ? $"'{Path.GetFileName(path)}' ja esta protegido: use o modo Remover senha."
                    : $"Senha incorreta para '{Path.GetFileName(path)}'.",
                ex);
        }
    }

    private static void Save(PdfDocument document, string inputPath, string outputPath)
    {
        if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
            throw new ConversionException("A saida nao pode ser o proprio arquivo de entrada.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        document.Save(outputPath);
    }
}
