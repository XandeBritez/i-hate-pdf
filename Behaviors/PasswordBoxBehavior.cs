using System.Windows;
using System.Windows.Controls;

namespace IHatePdf.Behaviors;

/// <summary>
/// O PasswordBox nao expoe Password como DependencyProperty (de proposito: a
/// senha nao fica pendurada na arvore visual). Este comportamento faz a ponte
/// para o ViewModel sem transformar o campo em TextBox.
/// </summary>
public static class PasswordBoxBehavior
{
    private static readonly DependencyProperty UpdatingProperty =
        DependencyProperty.RegisterAttached("Updating", typeof(bool), typeof(PasswordBoxBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword", typeof(string), typeof(PasswordBoxBehavior),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static string GetBoundPassword(DependencyObject element) =>
        (string)element.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject element, string value) =>
        element.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;

        box.PasswordChanged -= OnPasswordChanged;

        // Sem esta guarda, escrever de volta no ViewModel reposicionaria o
        // cursor no inicio a cada tecla digitada.
        if (!(bool)box.GetValue(UpdatingProperty))
            box.Password = e.NewValue as string ?? string.Empty;

        box.PasswordChanged += OnPasswordChanged;
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox box) return;

        box.SetValue(UpdatingProperty, true);
        SetBoundPassword(box, box.Password);
        box.SetValue(UpdatingProperty, false);
    }
}
