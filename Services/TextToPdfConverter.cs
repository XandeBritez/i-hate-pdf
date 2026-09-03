using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace IHatePdf.Services;

/// <summary>TXT -> PDF: le o conteudo e desenha o texto paginado com PDFsharp.</summary>
public sealed class TextToPdfConverter : IFileConverter
{
    private const double MarginPoints = 48;      // ~1,7 cm
    private const double FontSizePoints = 10.5;
    private const double LineHeightFactor = 1.35;

    public IReadOnlyCollection<string> Extensions { get; } = new[] { ".txt", ".log", ".csv", ".md" };

    public Task ConvertAsync(string inputPath, string outputPath, CancellationToken ct) =>
        Task.Run(() =>
        {
            var text = File.ReadAllText(inputPath, DetectEncoding(inputPath));

            using var document = new PdfDocument();
            document.Info.Title = Path.GetFileNameWithoutExtension(inputPath);

            var font = new XFont("Consolas", FontSizePoints);
            var lineHeight = font.GetHeight() * LineHeightFactor;

            PdfPage? page = null;
            XGraphics? gfx = null;
            double y = 0, usableWidth = 0, bottom = 0;

            void NewPage()
            {
                gfx?.Dispose();
                page = document.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                usableWidth = page.Width.Point - (2 * MarginPoints);
                bottom = page.Height.Point - MarginPoints;
                y = MarginPoints;
            }

            NewPage();

            foreach (var rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                ct.ThrowIfCancellationRequested();

                foreach (var line in WrapLine(rawLine.Replace("\t", "    "), gfx!, font, usableWidth))
                {
                    if (y + lineHeight > bottom)
                        NewPage();

                    gfx!.DrawString(line, font, XBrushes.Black,
                        new XRect(MarginPoints, y, usableWidth, lineHeight),
                        XStringFormats.TopLeft);

                    y += lineHeight;
                }
            }

            gfx?.Dispose();
            document.Save(outputPath);
        }, ct);

    /// <summary>Quebra a linha em pedacos que cabem na largura util, sem cortar palavras quando possivel.</summary>
    private static IEnumerable<string> WrapLine(string line, XGraphics gfx, XFont font, double maxWidth)
    {
        if (line.Length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        var current = new StringBuilder();
        foreach (var word in SplitKeepingSpaces(line))
        {
            var candidate = current.Length == 0 ? word : current + word;
            if (gfx.MeasureString(candidate, font).Width <= maxWidth)
            {
                current.Clear().Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                yield return current.ToString().TrimEnd();
                current.Clear();
            }

            // Palavra unica maior que a linha: quebra por caractere.
            var chunk = new StringBuilder();
            foreach (var ch in word)
            {
                if (gfx.MeasureString(chunk.ToString() + ch, font).Width > maxWidth && chunk.Length > 0)
                {
                    yield return chunk.ToString();
                    chunk.Clear();
                }
                chunk.Append(ch);
            }
            current.Append(chunk);
        }

        if (current.Length > 0)
            yield return current.ToString();
    }

    private static IEnumerable<string> SplitKeepingSpaces(string line)
    {
        var buffer = new StringBuilder();
        foreach (var ch in line)
        {
            buffer.Append(ch);
            if (ch == ' ')
            {
                yield return buffer.ToString();
                buffer.Clear();
            }
        }
        if (buffer.Length > 0)
            yield return buffer.ToString();
    }

    private static Encoding DetectEncoding(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> bom = stackalloc byte[4];
        var read = stream.Read(bom);

        if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return Encoding.UTF8;
        if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE) return Encoding.Unicode;
        if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF) return Encoding.BigEndianUnicode;
        return Encoding.UTF8;
    }
}
