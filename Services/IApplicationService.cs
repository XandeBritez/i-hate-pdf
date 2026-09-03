namespace IHatePdf.Services;

/// <summary>Controle do proprio processo, isolado para manter os ViewModels sem WPF.</summary>
public interface IApplicationService
{
    /// <summary>Caminho do executavel em execucao.</summary>
    string ExecutablePath { get; }

    /// <summary>Encerra o app (usado apos disparar o script de atualizacao).</summary>
    void Shutdown();
}
