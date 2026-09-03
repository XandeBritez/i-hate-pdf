namespace IHatePdf.Services;

/// <summary>
/// DOCX/XLSX -> PDF via LibreOffice headless.
/// Nao exige licenca comercial. Para trocar por Syncfusion basta registrar
/// outro IFileConverter para as mesmas extensoes.
/// </summary>
public sealed class LibreOfficeConverter : IFileConverter
{
    public IReadOnlyCollection<string> Extensions { get; } =
        new[] { ".docx", ".doc", ".xlsx", ".xls", ".pptx", ".ppt", ".odt", ".ods", ".rtf" };

    public Task ConvertAsync(string inputPath, string outputPath, CancellationToken ct) =>
        LibreOfficeRunner.ConvertAsync(inputPath, outputPath, "pdf", "pdf", inFilter: null, ct);

    /// <summary>Mantido para compatibilidade; delega ao executor compartilhado.</summary>
    public static string? FindSoffice() => LibreOfficeRunner.FindSoffice();
}
