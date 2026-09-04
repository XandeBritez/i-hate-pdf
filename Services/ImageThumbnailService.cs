using System.Windows.Media.Imaging;

namespace IHatePdf.Services;

/// <summary>Miniatura de arquivos de imagem (o PDF tem o seu proprio servico).</summary>
public interface IImageThumbnailService
{
    /// <summary>
    /// Devolve a miniatura ja congelada (Freeze), portanto segura para
    /// atravessar threads e ser bindada direto na UI.
    /// </summary>
    Task<BitmapSource> RenderAsync(string imagePath, int width = 220, CancellationToken ct = default);
}

/// <inheritdoc cref="IImageThumbnailService"/>
public sealed class ImageThumbnailService : IImageThumbnailService
{
    public Task<BitmapSource> RenderAsync(string imagePath, int width = 220, CancellationToken ct = default) =>
        Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            // DecodePixelWidth decodifica ja no tamanho do card: uma foto de
            // 12 MP nao precisa virar bitmap inteiro na memoria.
            bitmap.DecodePixelWidth = width;
            // OnLoad le tudo agora e solta o arquivo; sem isso a imagem ficaria
            // travada e o usuario nao conseguiria move-la ou apaga-la.
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.UriSource = new Uri(imagePath);
            bitmap.EndInit();
            bitmap.Freeze();

            return (BitmapSource)bitmap;
        }, ct);
}
