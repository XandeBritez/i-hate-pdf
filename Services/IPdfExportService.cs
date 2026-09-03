namespace IHatePdf.Services;

/// <summary>Formatos editaveis que um PDF pode virar.</summary>
public enum PdfExportFormat
{
    /// <summary>Word (.docx) — mantem a diagramacao aproximada. Exige LibreOffice.</summary>
    Word,

    /// <summary>Texto puro (.txt) — extracao nativa, nao depende de nada instalado.</summary>
    PlainText
}

/// <summary>Caminho inverso: PDF -> documento editavel.</summary>
public interface IPdfExportService
{
    /// <summary>true quando o LibreOffice esta instalado (necessario apenas para Word).</summary>
    bool IsLibreOfficeAvailable { get; }

    /// <summary>false quando falta a dependencia daquele formato; a UI avisa antes de tentar.</summary>
    bool IsFormatAvailable(PdfExportFormat format);

    /// <summary>Extensao de saida do formato, com ponto.</summary>
    string GetExtension(PdfExportFormat format);

    /// <summary>Converte o PDF e devolve o caminho do arquivo gerado.</summary>
    Task<string> ExportAsync(string pdfPath, string outputPath, PdfExportFormat format, CancellationToken ct = default);
}
