namespace IHatePdf.Services;

/// <summary>Conversao de arquivos de origem (TXT/DOCX/XLSX) para PDF.</summary>
public interface IConversionService
{
    /// <summary>Extensoes aceitas, em minusculas e com ponto (".txt", ".docx", ...).</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    bool CanConvert(string filePath);

    /// <summary>Converte o arquivo e devolve o caminho do PDF gerado.</summary>
    Task<string> ConvertToPdfAsync(string inputPath, string outputPath, CancellationToken ct = default);
}

/// <summary>Estrategia de conversao para um conjunto de extensoes.</summary>
public interface IFileConverter
{
    IReadOnlyCollection<string> Extensions { get; }
    Task ConvertAsync(string inputPath, string outputPath, CancellationToken ct);
}

/// <summary>Erro de conversao com mensagem apresentavel ao usuario.</summary>
public sealed class ConversionException : Exception
{
    public ConversionException(string message, Exception? inner = null) : base(message, inner) { }
}
