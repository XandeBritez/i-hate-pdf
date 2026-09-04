using Microsoft.Win32;

namespace IHatePdf.Services;

/// <summary>Entradas do app no menu de contexto do Explorer.</summary>
public interface IShellIntegrationService
{
    bool IsRegistered { get; }

    /// <summary>Cria as entradas. Escreve so em HKCU: nao pede elevacao.</summary>
    void Register();

    /// <summary>Remove tudo o que <see cref="Register"/> criou.</summary>
    void Unregister();
}

/// <summary>
/// Registro em HKEY_CURRENT_USER, de proposito: por usuario nao exige UAC e
/// combina com a instalacao em %LocalAppData%. Cada verbo chama o app com um
/// argumento que diz em qual tela abrir o arquivo.
/// </summary>
public sealed class ShellIntegrationService : IShellIntegrationService
{
    private const string Root = @"Software\Classes\SystemFileAssociations";
    private const string Prefix = "IHatePdf.";

    private static readonly (string Extension, string Verb, string Label, string Argument)[] Entries =
    [
        (".pdf", "Edit",     "Editar paginas no i HATE PDF", "--edit"),
        (".pdf", "Compress", "Comprimir com o i HATE PDF",   "--compress"),
        (".pdf", "Merge",    "Unir no i HATE PDF",           "--merge"),
        (".jpg", "ToPdf",    "Converter para PDF",           "--convert"),
        (".jpeg", "ToPdf",   "Converter para PDF",           "--convert"),
        (".png", "ToPdf",    "Converter para PDF",           "--convert"),
    ];

    private static string ExecutablePath =>
        Environment.ProcessPath ?? throw new InvalidOperationException("Executavel nao identificado.");

    public bool IsRegistered
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    $@"{Root}\.pdf\shell\{Prefix}Edit\command");

                var command = key?.GetValue(null) as string;

                // Instalacao movida ou atualizada muda o caminho: o registro
                // so vale se ainda apontar para este executavel.
                return command is not null &&
                       command.Contains(ExecutablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public void Register()
    {
        var exe = ExecutablePath;

        foreach (var (extension, verb, label, argument) in Entries)
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                $@"{Root}\{extension}\shell\{Prefix}{verb}");

            key.SetValue(null, label);
            key.SetValue("Icon", $"\"{exe}\",0");

            using var command = key.CreateSubKey("command");
            command.SetValue(null, $"\"{exe}\" {argument} \"%1\"");
        }
    }

    public void Unregister()
    {
        foreach (var (extension, verb, _, _) in Entries)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"{Root}\{extension}\shell\{Prefix}{verb}", throwOnMissingSubKey: false);
            }
            catch (Exception)
            {
                // Entrada ja removida a mao nao e problema.
            }
        }
    }
}
