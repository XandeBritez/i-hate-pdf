using IHatePdf.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace IHatePdf.Services;

/// <inheritdoc cref="IPdfService"/>
public sealed class PdfService : IPdfService
{
    public Task<int> GetPageCountAsync(string filePath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            using var doc = PdfReader.Open(filePath, PdfDocumentOpenMode.Import);
            return doc.PageCount;
        }, ct);

    public async Task<IReadOnlyList<PageReference>> GetPagesAsync(string filePath, CancellationToken ct = default)
    {
        var count = await GetPageCountAsync(filePath, ct).ConfigureAwait(false);
        return Enumerable.Range(0, count).Select(i => new PageReference(filePath, i)).ToList();
    }

    public async Task MergeAsync(IEnumerable<string> inputPaths, string outputPath, CancellationToken ct = default)
    {
        var pages = new List<PageReference>();
        foreach (var path in inputPaths)
            pages.AddRange(await GetPagesAsync(path, ct).ConfigureAwait(false));

        await BuildAsync(pages, outputPath, ct).ConfigureAwait(false);
    }

    public Task BuildAsync(IEnumerable<PageReference> pages, string outputPath, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            var ordered = pages.ToList();
            if (ordered.Count == 0)
                throw new InvalidOperationException("Nenhuma pagina selecionada para gravacao.");

            // Cache de documentos abertos: evita reabrir o mesmo arquivo por pagina.
            var opened = new Dictionary<string, PdfDocument>(StringComparer.OrdinalIgnoreCase);
            using var output = new PdfDocument();
            try
            {
                foreach (var page in ordered)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!opened.TryGetValue(page.SourcePath, out var source))
                    {
                        // Import: modo obrigatorio para copiar paginas entre documentos.
                        source = PdfReader.Open(page.SourcePath, PdfDocumentOpenMode.Import);
                        opened[page.SourcePath] = source;
                    }

                    if (page.PageIndex < 0 || page.PageIndex >= source.PageCount)
                        throw new IndexOutOfRangeException(
                            $"Pagina {page.PageIndex + 1} inexistente em '{Path.GetFileName(page.SourcePath)}'.");

                    var added = output.AddPage(source.Pages[page.PageIndex]);

                    if (page.Rotation != 0)
                    {
                        // O giro e somado ao que a pagina ja trazia: um documento
                        // pode chegar com /Rotate proprio, e substituir o valor
                        // enderecaria a pagina errada.
                        var rotation = (added.Rotate + page.Rotation) % 360;
                        added.Rotate = rotation < 0 ? rotation + 360 : rotation;
                    }
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                output.Save(outputPath);
            }
            finally
            {
                foreach (var doc in opened.Values)
                    doc.Dispose();
            }
        }, ct);

    public Task ExtractAsync(string inputPath, IEnumerable<int> pageIndexes, string outputPath, CancellationToken ct = default) =>
        BuildAsync(pageIndexes.Select(i => new PageReference(inputPath, i)), outputPath, ct);
}
