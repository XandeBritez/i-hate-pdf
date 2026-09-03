using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace IHatePdf.Behaviors;

/// <summary>
/// Comportamento anexado que transforma o drop de arquivos do Explorer em
/// comandos, mantendo o code-behind das Views vazio (MVVM estrito).
///
/// Uso:
///   b:FileDropBehavior.DropCommand="{Binding DropFilesCommand}"
///   b:FileDropBehavior.DragStateCommand="{Binding SetDragOverCommand}"
///
/// O estado de arraste vai para o ViewModel por ICommand (View -> VM) em vez de
/// uma DependencyProperty com OneWayToSource: escrever valor local numa DP
/// bindada com OneWayToSource apaga a expressao de binding na primeira escrita.
/// </summary>
public static class FileDropBehavior
{
    public static readonly DependencyProperty DropCommandProperty =
        DependencyProperty.RegisterAttached(
            "DropCommand", typeof(ICommand), typeof(FileDropBehavior),
            new PropertyMetadata(null, OnDropCommandChanged));

    public static ICommand? GetDropCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(DropCommandProperty);

    public static void SetDropCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(DropCommandProperty, value);

    /// <summary>Comando notificado com true/false quando arquivos entram ou saem da area.</summary>
    public static readonly DependencyProperty DragStateCommandProperty =
        DependencyProperty.RegisterAttached(
            "DragStateCommand", typeof(ICommand), typeof(FileDropBehavior),
            new PropertyMetadata(null));

    public static ICommand? GetDragStateCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(DragStateCommandProperty);

    public static void SetDragStateCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(DragStateCommandProperty, value);

    private static void OnDropCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        element.DragEnter -= OnDragOver;
        element.DragOver -= OnDragOver;
        element.DragLeave -= OnDragLeave;
        element.Drop -= OnDrop;

        if (e.NewValue is null)
        {
            element.AllowDrop = false;
            return;
        }

        element.AllowDrop = true;
        element.DragEnter += OnDragOver;
        element.DragOver += OnDragOver;
        element.DragLeave += OnDragLeave;
        element.Drop += OnDrop;
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        var hasFiles = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;

        if (hasFiles)
            NotifyDragState(sender, true);

        e.Handled = true;
    }

    private static void OnDragLeave(object sender, DragEventArgs e)
    {
        // DragLeave tambem dispara ao passar entre filhos: so limpa o estado
        // quando o ponteiro realmente saiu dos limites do elemento raiz.
        if (sender is FrameworkElement element)
        {
            var position = e.GetPosition(element);
            var inside = position.X >= 0 && position.Y >= 0 &&
                         position.X <= element.ActualWidth && position.Y <= element.ActualHeight;
            if (inside) return;
        }

        NotifyDragState(sender, false);
    }

    private static void OnDrop(object sender, DragEventArgs e)
    {
        NotifyDragState(sender, false);
        e.Handled = true;

        if (sender is not DependencyObject element) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;

        var command = GetDropCommand(element);
        IReadOnlyList<string> parameter = paths;

        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }

    private static void NotifyDragState(object sender, bool isDragOver)
    {
        if (sender is not DependencyObject element) return;

        var command = GetDragStateCommand(element);
        if (command?.CanExecute(isDragOver) == true)
            command.Execute(isDragOver);
    }
}
