using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media.Imaging;
using PDFtoImage;

namespace IHatePdf.Services;

/// <inheritdoc cref="IPdfRenderService"/>
public sealed class PdfRenderService : IPdfRenderService
{
    // Os bytes do PDF ficam em cache porque a grade do editor renderiza N paginas
    // do mesmo arquivo; reler o disco por miniatura mata a performance.
    private readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    // PDFium nao e reentrante: serializa as chamadas de renderizacao.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<BitmapSource> RenderPageAsync(string filePath, int pageIndex, int width = 220, CancellationToken ct = default)
    {
        var bytes = _cache.GetOrAdd(filePath, File.ReadAllBytes);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                using var png = new MemoryStream();
                Conversion.SavePng(
                    png,
                    bytes,
                    pageIndex,
                    password: null,
                    options: new RenderOptions(Width: width, WithAspectRatio: true, WithAnnotations: true));

                png.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;   // le tudo agora: o stream sera descartado
                bitmap.StreamSource = png;
                bitmap.EndInit();
                bitmap.Freeze();                                  // obrigatorio: cruza de thread para a UI
                return (BitmapSource)bitmap;
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate(string filePath) => _cache.TryRemove(filePath, out _);

    public void InvalidateAll() => _cache.Clear();
}
