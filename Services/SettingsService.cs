using System.Text.Json;
using System.Text.Json.Serialization;

namespace IHatePdf.Services;

/// <summary>O que o app lembra entre sessoes.</summary>
public sealed class AppSettings
{
    public string Theme { get; set; } = "Light";

    public string? ConverterOutputFolder { get; set; }
    public bool CombineImages { get; set; }

    public string? CompressOutputFolder { get; set; }
    public bool CompressRasterize { get; set; }
    public string CompressStrength { get; set; } = nameof(CompressionStrength.Balanced);

    public string? SecurityOutputFolder { get; set; }

    public string? ExportOutputFolder { get; set; }
    public string ExportFormat { get; set; } = nameof(PdfExportFormat.Word);

    /// <summary>Pasta padrao usada quando nada foi escolhido ainda.</summary>
    public static string DefaultOutputFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "IHatePdf");
}

public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Agenda a gravacao; chamadas seguidas viram uma so.</summary>
    void Save();

    /// <summary>Grava imediatamente (usado ao fechar o app).</summary>
    void Flush();
}

/// <summary>
/// Preferencias em JSON dentro de %AppData%\IHatePdf.
/// Fica fora da pasta do app de proposito: a atualizacao automatica troca o
/// executavel, e configuracao ao lado dele seria perdida ou sobrescrita.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;
    private readonly Timer _debounce;
    private readonly object _gate = new();  // System.Threading.Lock so existe no .NET 9

    public SettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IHatePdf");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "settings.json");

        Current = Load(_path);

        // Trocar de opcao na UI dispara varias mudancas seguidas; gravar so
        // depois que elas param evita escrever o arquivo a cada tecla.
        _debounce = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public AppSettings Current { get; }

    private static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Arquivo corrompido nao pode impedir o app de abrir: cai no padrao.
        }

        return new AppSettings();
    }

    public void Save() => _debounce.Change(TimeSpan.FromMilliseconds(600), Timeout.InfiniteTimeSpan);

    public void Flush()
    {
        lock (_gate)
        {
            try
            {
                File.WriteAllText(_path, JsonSerializer.Serialize(Current, JsonOptions));
            }
            catch (Exception)
            {
                // Perder preferencia e aceitavel; travar o app por causa disso nao.
            }
        }
    }

    public void Dispose()
    {
        _debounce.Dispose();
        Flush();
    }
}
