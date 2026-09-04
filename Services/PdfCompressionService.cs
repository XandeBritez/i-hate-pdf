using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PDFtoImage;
using SkiaSharp;
// PdfPig e PDFsharp tem um PdfDocument cada: o alias evita a ambiguidade.
using PigDocument = UglyToad.PdfPig.PdfDocument;

namespace IHatePdf.Services;

/// <summary>
/// Compressao sem depender de Ghostscript: usa o que o app ja carrega —
/// PDFsharp para reescrever a estrutura e PDFium para rasterizar.
/// </summary>
public sealed class PdfCompressionService : IPdfCompressionService
{
    // Serializa o PDFium, que nao e reentrante (mesma razao do servico de miniaturas).
    private static readonly SemaphoreSlim PdfiumGate = new(1, 1);

    private static (int Dpi, int Quality) Preset(CompressionStrength strength) => strength switch
    {
        CompressionStrength.High => (200, 85),
        CompressionStrength.Balanced => (150, 72),
        CompressionStrength.Maximum => (110, 58),
        _ => (150, 72)
    };

    public Task<bool> HasExtractableTextAsync(string pdfPath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            try
            {
                using var document = PigDocument.Open(pdfPath);
                // Poucas paginas bastam para saber se e um documento de texto
                // ou uma digitalizacao.
                var limit = Math.Min(document.NumberOfPages, 5);
                for (var i = 1; i <= limit; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!string.IsNullOrWhiteSpace(document.GetPage(i).Text))
                        return true;
                }
            }
            catch
            {
                // PDF ilegivel para o PdfPig nao impede a compressao.
            }

            return false;
        }, ct);

    public async Task<CompressionResult> CompressAsync(
        string inputPath,
        string outputPath,
        CompressionMode mode,
        CompressionStrength strength,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(inputPath))
            throw new ConversionException($"Arquivo nao encontrado: {inputPath}");

        if (!string.Equals(Path.GetExtension(inputPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ConversionException("Esta ferramenta aceita apenas arquivos PDF.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var originalBytes = new FileInfo(inputPath).Length;

        // Grava em arquivo temporario: so vira a saida final se realmente encolher.
        var temp = outputPath + ".tmp";

        try
        {
            if (mode == CompressionMode.Lossless)
                await Task.Run(() => Rewrite(inputPath, temp, ct), ct).ConfigureAwait(false);
            else
                await RasterizeAsync(inputPath, temp, strength, progress, ct).ConfigureAwait(false);

            var compressedBytes = new FileInfo(temp).Length;

            // Comprimir e aumentar o arquivo e um resultado possivel (PDF ja
            // otimizado, ou pagina de texto virando imagem). Nesse caso o
            // original e preservado em vez de entregar algo pior.
            if (compressedBytes >= originalBytes)
            {
                File.Delete(temp);
                File.Copy(inputPath, outputPath, overwrite: true);

                return new CompressionResult(
                    outputPath, originalBytes, originalBytes,
                    KeptText: true, Improved: false);
            }

            File.Move(temp, outputPath, overwrite: true);

            return new CompressionResult(
                outputPath, originalBytes, compressedBytes,
                KeptText: mode == CompressionMode.Lossless,
                Improved: true);
        }
        finally
        {
            if (File.Exists(temp))
                try { File.Delete(temp); } catch { /* limpeza best-effort */ }
        }
    }

    /// <summary>Reescreve o documento com os fluxos comprimidos, sem tocar no conteudo.</summary>
    private static void Rewrite(string inputPath, string outputPath, CancellationToken ct)
    {
        using var source = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import);
        using var output = new PdfDocument();

        output.Options.CompressContentStreams = true;
        output.Options.NoCompression = false;
        output.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
        output.Options.EnableCcittCompressionForBilevelImages = true;

        for (var i = 0; i < source.PageCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            output.AddPage(source.Pages[i]);
        }

        output.Save(outputPath);
    }

    /// <summary>Renderiza cada pagina e regrava como JPEG dentro de um PDF novo.</summary>
    private async Task RasterizeAsync(
        string inputPath,
        string outputPath,
        CompressionStrength strength,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var (dpi, quality) = Preset(strength);
        var bytes = await File.ReadAllBytesAsync(inputPath, ct).ConfigureAwait(false);

        await PdfiumGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await Task.Run(() =>
            {
                var pageCount = Conversion.GetPageCount(bytes);

                using var output = new PdfDocument();
                output.Options.CompressContentStreams = true;
                output.Options.NoCompression = false;

                // O XImage le do stream durante o Save, entao os streams
                // precisam continuar vivos ate o documento ser gravado.
                var openStreams = new List<MemoryStream>(pageCount);

                try
                {
                    for (var i = 0; i < pageCount; i++)
                    {
                        ct.ThrowIfCancellationRequested();

                        using var bitmap = Conversion.ToImage(
                            bytes, i, password: null, options: new RenderOptions(Dpi: dpi));

                        using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, quality);

                        var jpeg = new MemoryStream(encoded.ToArray());
                        openStreams.Add(jpeg);

                        // O tamanho vem do bitmap renderizado, nao do MediaBox:
                        // assim paginas com /Rotate saem na orientacao correta.
                        var page = output.AddPage();
                        page.Width = XUnit.FromPoint(bitmap.Width * 72d / dpi);
                        page.Height = XUnit.FromPoint(bitmap.Height * 72d / dpi);

                        using var gfx = XGraphics.FromPdfPage(page);
                        var image = XImage.FromStream(jpeg);
                        gfx.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);

                        progress?.Report((i + 1) * 100d / pageCount);
                    }

                    output.Save(outputPath);
                }
                finally
                {
                    foreach (var stream in openStreams)
                        stream.Dispose();
                }
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            PdfiumGate.Release();
        }
    }
}
