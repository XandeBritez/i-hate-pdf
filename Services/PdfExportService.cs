using System.Text;
using PDFtoImage;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace IHatePdf.Services;

/// <summary>
/// PDF -> Word/TXT.
///
/// Word: LibreOffice com --infilter=writer_pdf_import. Sem esse filtro o PDF
/// abriria no Draw e a saida viria como caixas de texto soltas.
///
/// TXT: extracao nativa com PdfPig, sem depender de nada instalado.
/// O LibreOffice NAO serve para gerar .txt aqui: ao importar um PDF ele
/// coloca o conteudo em quadros de texto flutuantes, e o filtro de texto
/// puro exporta apenas o corpo do documento — o arquivo saia vazio.
///
/// Limite do formato, nao da implementacao: o PDF descreve posicoes de
/// glifos, nao paragrafos. Texto e recuperado bem; layout complexo (colunas,
/// tabelas) e aproximado. PDF escaneado nao contem texto algum e exigiria
/// OCR, que este app nao faz — nesse caso a conversao falha com aviso claro.
/// </summary>
public sealed class PdfExportService : IPdfExportService
{
    public bool IsLibreOfficeAvailable => LibreOfficeRunner.IsInstalled;

    public bool IsFormatAvailable(PdfExportFormat format) => format switch
    {
        // So o .docx depende do LibreOffice; texto e imagens sao nativos.
        PdfExportFormat.Word => IsLibreOfficeAvailable,
        _ => true
    };

    public bool ProducesFolder(PdfExportFormat format) =>
        format is PdfExportFormat.PngImages or PdfExportFormat.JpegImages;

    public string GetExtension(PdfExportFormat format) => format switch
    {
        PdfExportFormat.Word => ".docx",
        PdfExportFormat.PlainText => ".txt",
        // A saida de imagens e uma pasta: sem extensao no caminho.
        PdfExportFormat.PngImages or PdfExportFormat.JpegImages => string.Empty,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public async Task<string> ExportAsync(string pdfPath, string outputPath, PdfExportFormat format, CancellationToken ct = default)
    {
        if (!File.Exists(pdfPath))
            throw new ConversionException($"Arquivo nao encontrado: {pdfPath}");

        if (!string.Equals(Path.GetExtension(pdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ConversionException("Esta ferramenta converte apenas arquivos PDF.");

        if (!ProducesFolder(format))
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        if (format == PdfExportFormat.PlainText)
        {
            await ExtractTextAsync(pdfPath, outputPath, ct).ConfigureAwait(false);
            return outputPath;
        }

        if (ProducesFolder(format))
        {
            await ExportImagesAsync(pdfPath, outputPath, format, ct).ConfigureAwait(false);
            return outputPath;
        }

        await LibreOfficeRunner
            .ConvertAsync(pdfPath, outputPath, "docx:MS Word 2007 XML", "docx", inFilter: "writer_pdf_import", ct)
            .ConfigureAwait(false);

        return outputPath;
    }

    /// <summary>Renderiza cada pagina como imagem dentro de uma pasta.</summary>
    private static Task ExportImagesAsync(string pdfPath, string outputFolder, PdfExportFormat format, CancellationToken ct) =>
        Task.Run(() =>
        {
            Directory.CreateDirectory(outputFolder);

            var bytes = File.ReadAllBytes(pdfPath);
            var pageCount = Conversion.GetPageCount(bytes);
            var png = format == PdfExportFormat.PngImages;

            for (var i = 0; i < pageCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                using var bitmap = Conversion.ToImage(bytes, i, password: null, options: new RenderOptions(Dpi: 150));
                using var encoded = bitmap.Encode(
                    png ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg,
                    png ? 100 : 88);

                // Numero com zeros a esquerda para o Explorer ordenar certo.
                var name = $"pagina-{i + 1:D3}.{(png ? "png" : "jpg")}";
                using var file = File.Create(Path.Combine(outputFolder, name));
                encoded.SaveTo(file);
            }
        }, ct);

    /// <summary>Extrai o texto pagina a pagina, na ordem de leitura do conteudo.</summary>
    private static Task ExtractTextAsync(string pdfPath, string outputPath, CancellationToken ct) =>
        Task.Run(() =>
        {
            var builder = new StringBuilder();
            var pagesWithText = 0;

            using (var document = PdfDocument.Open(pdfPath))
            {
                var total = document.NumberOfPages;

                for (var number = 1; number <= total; number++)
                {
                    ct.ThrowIfCancellationRequested();

                    var page = document.GetPage(number);

                    // ContentOrderTextExtractor respeita a ordem do conteudo;
                    // Page.Text devolveria os glifos na ordem em que aparecem no
                    // fluxo, o que embaralha documentos de varias colunas.
                    var text = ContentOrderTextExtractor.GetText(page, true);

                    if (!string.IsNullOrWhiteSpace(text))
                        pagesWithText++;

                    if (number > 1)
                        builder.AppendLine().AppendLine();

                    builder.AppendLine($"--- Pagina {number} de {total} ---").AppendLine();
                    builder.Append(text?.TrimEnd());
                }
            }

            if (pagesWithText == 0)
                throw new ConversionException(
                    "Nenhum texto encontrado neste PDF. Provavelmente e um documento " +
                    "escaneado (imagem), que precisaria de OCR — recurso que este app nao tem.");

            File.WriteAllText(outputPath, builder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }, ct);
}
