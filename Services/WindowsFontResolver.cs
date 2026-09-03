using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace IHatePdf.Services;

/// <summary>
/// Resolvedor de fontes do PDFsharp que le os arquivos TTF de %WINDIR%\Fonts.
///
/// Sem isto, XFont lanca "No appropriate font found for family name ...":
/// o PDFsharp 6 nao embute fontes e nao resolve familias do Windows sozinho.
/// </summary>
public sealed class WindowsFontResolver : IFontResolver
{
    private static readonly string FontsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");

    /// <summary>familia (minuscula) -> nomes de arquivo por variacao [regular, bold, italic, boldItalic].</summary>
    private static readonly Dictionary<string, string[]> Families = new(StringComparer.OrdinalIgnoreCase)
    {
        ["consolas"] = ["consola.ttf", "consolab.ttf", "consolai.ttf", "consolaz.ttf"],
        ["courier new"] = ["cour.ttf", "courbd.ttf", "couri.ttf", "courbi.ttf"],
        ["arial"] = ["arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"],
        ["segoe ui"] = ["segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf"],
        ["times new roman"] = ["times.ttf", "timesbd.ttf", "timesi.ttf", "timesbi.ttf"],
        ["calibri"] = ["calibri.ttf", "calibrib.ttf", "calibrii.ttf", "calibriz.ttf"],
        ["verdana"] = ["verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf"]
    };

    private const string FallbackFamily = "arial";

    private readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var family = Families.ContainsKey(familyName) ? familyName : FallbackFamily;
        var variant = (isBold, isItalic) switch
        {
            (false, false) => 0,
            (true, false) => 1,
            (false, true) => 2,
            (true, true) => 3
        };

        var file = Families[family][variant];
        if (!File.Exists(Path.Combine(FontsDir, file)))
        {
            // Variacao ausente: cai para o regular da mesma familia.
            file = Families[family][0];
            if (!File.Exists(Path.Combine(FontsDir, file)))
            {
                file = Families[FallbackFamily][0];
                if (!File.Exists(Path.Combine(FontsDir, file)))
                    return null;
            }
        }

        return new FontResolverInfo(file);
    }

    public byte[]? GetFont(string faceName)
    {
        if (_cache.TryGetValue(faceName, out var cached))
            return cached;

        var path = Path.Combine(FontsDir, faceName);
        if (!File.Exists(path))
            return null;

        var bytes = File.ReadAllBytes(path);
        _cache[faceName] = bytes;
        return bytes;
    }
}
