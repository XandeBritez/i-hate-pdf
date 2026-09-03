namespace IHatePdf.Services;

/// <summary>
/// PDF -> Word/TXT pelo LibreOffice, forcando a importacao no Writer
/// (--infilter=writer_pdf_import). Sem esse filtro o PDF abriria no Draw e a
/// saida viria como um amontoado de caixas de texto posicionadas.
///
/// Limite do formato, nao da implementacao: o PDF descreve posicoes de glifos,
/// nao paragrafos. A reconstrucao e uma aproximacao — texto e recuperado bem,
/// layout complexo (colunas, tabelas) nem sempre. PDF escaneado nao tem texto
/// algum e exigiria OCR, que este app nao faz.
/// </summary>
public sealed class PdfExportService : IPdfExportService
{
    public bool IsAvailable => LibreOfficeRunner.IsInstalled;

    public string GetExtension(PdfExportFormat format) => format switch
    {
        PdfExportFormat.Word => ".docx",
        PdfExportFormat.PlainText => ".txt",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public async Task<string> ExportAsync(string pdfPath, string outputPath, PdfExportFormat format, CancellationToken ct = default)
    {
        if (!File.Exists(pdfPath))
            throw new ConversionException($"Arquivo nao encontrado: {pdfPath}");

        if (!string.Equals(Path.GetExtension(pdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ConversionException("Esta ferramenta converte apenas arquivos PDF.");

        var (convertTo, extension) = format switch
        {
            PdfExportFormat.Word => ("docx:MS Word 2007 XML", "docx"),
            PdfExportFormat.PlainText => ("txt:Text (encoded):UTF8", "txt"),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        await LibreOfficeRunner
            .ConvertAsync(pdfPath, outputPath, convertTo, extension, inFilter: "writer_pdf_import", ct)
            .ConfigureAwait(false);

        return outputPath;
    }
}
