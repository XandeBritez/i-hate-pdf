namespace IHatePdf.Models;

/// <summary>
/// Ponteiro imutavel para uma pagina de um PDF de origem.
/// Toda operacao do editor (deletar, reordenar, inserir) e apenas
/// uma transformacao sobre uma lista de PageReference.
/// </summary>
/// <param name="SourcePath">Caminho absoluto do PDF de origem.</param>
/// <param name="PageIndex">Indice da pagina na origem (base zero).</param>
public readonly record struct PageReference(string SourcePath, int PageIndex);
