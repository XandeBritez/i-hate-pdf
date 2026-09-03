using System.Windows;
using CommunityToolkit.Mvvm.Messaging;
using IHatePdf.Services;
using IHatePdf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Fonts;

namespace IHatePdf;

public partial class App : Application
{
    private IServiceProvider _services = null!;

    public static IServiceProvider Services => ((App)Current)._services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // PDFsharp 6 nao resolve fontes sozinho: sem isto, XFont lanca
        // "No appropriate font found for family name ..." na conversao de TXT.
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        GlobalFontSettings.FontResolver = new WindowsFontResolver();

        _services = ConfigureServices();

        // Erros nao tratados na UI viram dialogo em vez de crash silencioso.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Erro inesperado", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var messenger = _services.GetRequiredService<IMessenger>();
        messenger.Register<ThemeChangedMessage>(this, (_, message) => ApplyTheme(message.Value));

        var window = new MainWindow { DataContext = _services.GetRequiredService<MainViewModel>() };
        MainWindow = window;
        window.Show();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Servicos
        services.AddSingleton<IPdfService, PdfService>();
        services.AddSingleton<IPdfRenderService, PdfRenderService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();

        // Estrategias de conversao: trocar LibreOfficeConverter por um conversor
        // Syncfusion aqui muda o backend de DOCX/XLSX sem tocar no resto do app.
        services.AddSingleton<IFileConverter, TextToPdfConverter>();
        services.AddSingleton<IFileConverter, LibreOfficeConverter>();
        services.AddSingleton<IConversionService, ConversionService>();

        // ViewModels
        services.AddSingleton<MergeViewModel>();
        services.AddSingleton<EditorViewModel>();
        services.AddSingleton<ConverterViewModel>();
        services.AddSingleton<AboutViewModel>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>Troca a paleta (indice 0 dos dicionarios mesclados) sem recriar a janela.</summary>
    private void ApplyTheme(AppTheme theme)
    {
        var source = theme == AppTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
        Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative)
        };
    }
}
