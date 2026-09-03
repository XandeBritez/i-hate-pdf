using IHatePdf.Models;

namespace IHatePdf.Services;

/// <summary>
/// Logica pura de PDF. Todas as operacoes de pagina (merge, remocao, reordenacao,
/// extracao) sao expressas como a construcao de um documento a partir de uma
/// sequencia ordenada de <see cref="PageReference"/>.
/// </summary>
public interface IPdfService
{
    /// <summary>Numero de paginas de um PDF.</summary>
    Task<int> GetPageCountAsync(string filePath, CancellationToken ct = default);

    /// <summary>Todas as paginas de um arquivo, na ordem original.</summary>
    Task<IReadOnlyList<PageReference>> GetPagesAsync(string filePath, CancellationToken ct = default);

    /// <summary>Unifica varios PDFs, na ordem informada, em um unico arquivo.</summary>
    Task MergeAsync(IEnumerable<string> inputPaths, string outputPath, CancellationToken ct = default);

    /// <summary>
    /// Grava um PDF contendo exatamente as paginas informadas, na ordem informada.
    /// Primitiva usada por deletar / reordenar / inserir paginas.
    /// </summary>
    Task BuildAsync(IEnumerable<PageReference> pages, string outputPath, CancellationToken ct = default);

    /// <summary>Extrai um subconjunto de paginas de um unico arquivo para um novo PDF.</summary>
    Task ExtractAsync(string inputPath, IEnumerable<int> pageIndexes, string outputPath, CancellationToken ct = default);
}
