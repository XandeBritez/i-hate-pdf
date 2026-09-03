using System.Windows;

namespace IHatePdf.Services;

/// <inheritdoc cref="IApplicationService"/>
public sealed class ApplicationService : IApplicationService
{
    public string ExecutablePath =>
        Environment.ProcessPath
        ?? throw new InvalidOperationException("Nao foi possivel determinar o executavel em execucao.");

    public void Shutdown() =>
        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
}
