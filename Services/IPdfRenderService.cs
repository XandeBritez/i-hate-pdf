using System.Windows.Media.Imaging;

namespace IHatePdf.Services;

/// <summary>Geracao de bitmaps/miniaturas a partir das paginas de um PDF (PDFium).</summary>
public interface IPdfRenderService
{
    /// <summary>
    /// Renderiza uma pagina como <see cref="BitmapSource"/> ja congelado (Freeze),
    /// portanto seguro para atravessar threads e ser bindado direto na UI.
    /// </summary>
    /// <param name="filePath">PDF de origem.</param>
    /// <param name="pageIndex">Indice da pagina (base zero).</param>
    /// <param name="width">Largura alvo em pixels; a altura segue o aspect ratio.</param>
    Task<BitmapSource> RenderPageAsync(string filePath, int pageIndex, int width = 220, CancellationToken ct = default);

    /// <summary>Descarta os bytes em cache de um arquivo (chamar apos sobrescrever o PDF).</summary>
    void Invalidate(string filePath);

    /// <summary>Descarta todo o cache.</summary>
    void InvalidateAll();
}
