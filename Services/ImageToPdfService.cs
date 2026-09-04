using PdfSharp.Drawing;
using PdfSharp.Pdf;
using SkiaSharp;

namespace IHatePdf.Services;

/// <summary>Fotos e imagens viram paginas de PDF.</summary>
public interface IImageToPdfService
{
    /// <summary>Extensoes de imagem aceitas.</summary>
    IReadOnlyCollection<string> Extensions { get; }

    bool IsImage(string path);

    /// <summary>Uma imagem, um PDF de uma pagina.</summary>
    Task ConvertAsync(string imagePath, string outputPath, CancellationToken ct = default);

    /// <summary>Varias imagens, um PDF unico com uma pagina por imagem, na ordem recebida.</summary>
    Task CombineAsync(IEnumerable<string> imagePaths, string outputPath, CancellationToken ct = default);
}

/// <summary>
/// Decodifica com SkiaSharp (que le webp, heic-lite, gif, bmp, tiff basico
/// alem de jpeg e png) e regrava como JPEG dentro do PDF. Passar o arquivo
/// direto ao PDFsharp limitaria os formatos aceitos.
/// </summary>
public sealed class ImageToPdfService : IImageToPdfService
{
    // A4 em pontos; a pagina acompanha a orientacao da imagem.
    private const double A4Width = 595.28;
    private const double A4Height = 841.89;
    private const double Margin = 24;
    private const int JpegQuality = 90;

    public IReadOnlyCollection<string> Extensions { get; } =
        new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff" };

    public bool IsImage(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public Task ConvertAsync(string imagePath, string outputPath, CancellationToken ct = default) =>
        CombineAsync(new[] { imagePath }, outputPath, ct);

    public Task CombineAsync(IEnumerable<string> imagePaths, string outputPath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var paths = imagePaths.ToList();
            if (paths.Count == 0)
                throw new ConversionException("Nenhuma imagem para converter.");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            using var document = new PdfDocument();
            document.Info.Title = Path.GetFileNameWithoutExtension(outputPath);
            document.Options.CompressContentStreams = true;

            // O XImage le do stream durante o Save: os streams precisam
            // continuar abertos ate o documento ser gravado.
            var openStreams = new List<MemoryStream>(paths.Count);

            try
            {
                foreach (var path in paths)
                {
                    ct.ThrowIfCancellationRequested();
                    AddPage(document, path, openStreams);
                }

                document.Save(outputPath);
            }
            finally
            {
                foreach (var stream in openStreams)
                    stream.Dispose();
            }
        }, ct);

    private static void AddPage(PdfDocument document, string imagePath, List<MemoryStream> openStreams)
    {
        using var original = SKBitmap.Decode(imagePath)
            ?? throw new ConversionException($"Nao foi possivel ler a imagem '{Path.GetFileName(imagePath)}'.");

        // JPEG nao tem transparencia: o alfa e resolvido sobre branco em vez
        // de virar preto, que e o que acontece ao encodar direto.
        using var flattened = new SKBitmap(original.Width, original.Height, SKColorType.Rgb888x, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(flattened))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(original, 0, 0, new SKSamplingOptions(SKFilterMode.Linear), paint: null);
        }

        using var encoded = flattened.Encode(SKEncodedImageFormat.Jpeg, JpegQuality)
            ?? throw new ConversionException($"Falha ao comprimir '{Path.GetFileName(imagePath)}'.");

        var jpeg = new MemoryStream(encoded.ToArray());
        openStreams.Add(jpeg);

        var landscape = original.Width > original.Height;
        var page = document.AddPage();
        page.Width = XUnit.FromPoint(landscape ? A4Height : A4Width);
        page.Height = XUnit.FromPoint(landscape ? A4Width : A4Height);

        var maxWidth = page.Width.Point - (2 * Margin);
        var maxHeight = page.Height.Point - (2 * Margin);

        // Encaixa sem distorcer e sem ampliar imagem pequena alem da pagina.
        var scale = Math.Min(maxWidth / original.Width, maxHeight / original.Height);
        var width = original.Width * scale;
        var height = original.Height * scale;

        using var gfx = XGraphics.FromPdfPage(page);
        var image = XImage.FromStream(jpeg);
        gfx.DrawImage(image, (page.Width.Point - width) / 2, (page.Height.Point - height) / 2, width, height);
    }
}

/// <summary>Liga as imagens ao despacho por extensao do conversor.</summary>
public sealed class ImageToPdfConverter : IFileConverter
{
    private readonly IImageToPdfService _service;

    public ImageToPdfConverter(IImageToPdfService service) => _service = service;

    public IReadOnlyCollection<string> Extensions => _service.Extensions;

    public Task ConvertAsync(string inputPath, string outputPath, CancellationToken ct) =>
        _service.ConvertAsync(inputPath, outputPath, ct);
}
