namespace IHatePdf.Services;

/// <summary>
/// Despacha a conversao para a estrategia registrada da extensao.
/// Trocar o backend de DOCX/XLSX (LibreOffice -> Syncfusion) e substituir
/// um <see cref="IFileConverter"/> no registro de DI.
/// </summary>
public sealed class ConversionService : IConversionService
{
    private readonly Dictionary<string, IFileConverter> _converters;

    public ConversionService(IEnumerable<IFileConverter> converters)
    {
        _converters = new Dictionary<string, IFileConverter>(StringComparer.OrdinalIgnoreCase);
        foreach (var converter in converters)
            foreach (var ext in converter.Extensions)
                _converters[ext] = converter;
    }

    public IReadOnlyCollection<string> SupportedExtensions => _converters.Keys;

    public bool CanConvert(string filePath) => _converters.ContainsKey(Path.GetExtension(filePath));

    public async Task<string> ConvertToPdfAsync(string inputPath, string outputPath, CancellationToken ct = default)
    {
        if (!File.Exists(inputPath))
            throw new ConversionException($"Arquivo nao encontrado: {inputPath}");

        var ext = Path.GetExtension(inputPath);
        if (!_converters.TryGetValue(ext, out var converter))
            throw new ConversionException($"Extensao nao suportada: {ext}");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await converter.ConvertAsync(inputPath, outputPath, ct).ConfigureAwait(false);

        if (!File.Exists(outputPath))
            throw new ConversionException($"A conversao terminou sem gerar o PDF de saida: {outputPath}");

        return outputPath;
    }
}
