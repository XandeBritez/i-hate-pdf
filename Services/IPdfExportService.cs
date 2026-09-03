namespace IHatePdf.Services;

/// <summary>Formatos editaveis que um PDF pode virar.</summary>
public enum PdfExportFormat
{
    /// <summary>Word (.docx) — texto fluido, melhor para reeditar o conteudo.</summary>
    Word,

    /// <summary>Texto puro (.txt) — so o conteudo textual, sem formatacao.</summary>
    PlainText
}

/// <summary>Caminho inverso: PDF -> documento editavel.</summary>
public interface IPdfExportService
{
    /// <summary>false quando o LibreOffice nao esta instalado; a UI avisa antes de tentar.</summary>
    bool IsAvailable { get; }

    /// <summary>Extensao de saida do formato, com ponto.</summary>
    string GetExtension(PdfExportFormat format);

    /// <summary>Converte o PDF e devolve o caminho do arquivo gerado.</summary>
    Task<string> ExportAsync(string pdfPath, string outputPath, PdfExportFormat format, CancellationToken ct = default);
}
