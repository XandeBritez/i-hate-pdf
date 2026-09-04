namespace IHatePdf.Models;

/// <summary>
/// Ponteiro imutavel para uma pagina de um PDF de origem.
/// Toda operacao do editor (deletar, reordenar, inserir, girar) e apenas
/// uma transformacao sobre uma lista de PageReference.
/// </summary>
/// <param name="SourcePath">Caminho absoluto do PDF de origem.</param>
/// <param name="PageIndex">Indice da pagina na origem (base zero).</param>
/// <param name="Rotation">Giro a aplicar, em graus horarios: 0, 90, 180 ou 270.</param>
public readonly record struct PageReference(string SourcePath, int PageIndex, int Rotation = 0);
