namespace IHatePdf.Services;

/// <summary>Como o arquivo sera reduzido.</summary>
public enum CompressionMode
{
    /// <summary>
    /// Reescreve o documento com os fluxos comprimidos e sem objetos orfaos.
    /// O texto continua selecionavel e nada e degradado; o ganho depende de
    /// quanto o gerador original desperdicou.
    /// </summary>
    Lossless,

    /// <summary>
    /// Converte cada pagina em imagem JPEG. Encolhe muito documentos
    /// digitalizados, mas o resultado deixa de ter texto selecionavel.
    /// </summary>
    Rasterize
}

/// <summary>Preajustes de qualidade do modo <see cref="CompressionMode.Rasterize"/>.</summary>
public enum CompressionStrength
{
    /// <summary>200 dpi, JPEG 85 — para imprimir.</summary>
    High,

    /// <summary>150 dpi, JPEG 72 — leitura em tela.</summary>
    Balanced,

    /// <summary>110 dpi, JPEG 58 — o menor arquivo possivel.</summary>
    Maximum
}

/// <param name="OutputPath">Arquivo gerado.</param>
/// <param name="OriginalBytes">Tamanho de entrada.</param>
/// <param name="CompressedBytes">Tamanho de saida.</param>
/// <param name="KeptText">false quando o documento virou imagem.</param>
/// <param name="Improved">false quando o original ja era menor e foi mantido.</param>
public sealed record CompressionResult(
    string OutputPath,
    long OriginalBytes,
    long CompressedBytes,
    bool KeptText,
    bool Improved)
{
    /// <summary>Percentual economizado (0 quando nao houve ganho).</summary>
    public double SavedPercent =>
        OriginalBytes <= 0 ? 0 : Math.Max(0, (OriginalBytes - CompressedBytes) * 100d / OriginalBytes);
}

/// <summary>Reducao do tamanho de arquivos PDF.</summary>
public interface IPdfCompressionService
{
    /// <summary>true quando o PDF tem texto extraivel — rasterizar o perderia.</summary>
    Task<bool> HasExtractableTextAsync(string pdfPath, CancellationToken ct = default);

    Task<CompressionResult> CompressAsync(
        string inputPath,
        string outputPath,
        CompressionMode mode,
        CompressionStrength strength,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}
