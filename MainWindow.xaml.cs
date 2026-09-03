using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IHatePdf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        FitToWorkArea();
    }

    /// <summary>
    /// Mantem a janela dentro da area util do monitor (barra de tarefas descontada),
    /// para que os botoes minimizar/maximizar/fechar fiquem sempre visiveis em telas baixas.
    /// </summary>
    private void FitToWorkArea()
    {
        var work = SystemParameters.WorkArea;

        Height = Math.Min(Height, work.Height * 0.92);
        Width = Math.Min(Width, work.Width * 0.94);

        MinHeight = Math.Min(MinHeight, Height);
        MinWidth = Math.Min(MinWidth, Width);
    }
}