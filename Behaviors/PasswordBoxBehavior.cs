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

    private static readonly DependencyProperty AttachedProperty =
        DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(PasswordBoxBehavior),
            new PropertyMetadata(false));

    // O valor padrao e null, e nao string.Empty, de proposito: o ViewModel
    // costuma comecar com string vazia e, com os dois iguais, a propriedade
    // nunca "mudaria" — o callback abaixo jamais rodaria e o campo ficaria
    // mudo, aceitando digitacao sem nunca avisar o ViewModel.
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword", typeof(string), typeof(PasswordBoxBehavior),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    public static string? GetBoundPassword(DependencyObject element) =>
        (string?)element.GetValue(BoundPasswordProperty);

    public static void SetBoundPassword(DependencyObject element, string? value) =>
        element.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box) return;

        Attach(box);

        // Sem esta guarda, escrever de volta no ViewModel reposicionaria o
        // cursor no inicio a cada tecla digitada.
        if ((bool)box.GetValue(UpdatingProperty)) return;

        var value = e.NewValue as string ?? string.Empty;
        if (box.Password != value)
            box.Password = value;
    }

    /// <summary>
    /// Liga o evento uma unica vez. Tambem no Loaded, para o caso de a
    /// propriedade nunca mudar de valor depois do binding inicial.
    /// </summary>
    private static void Attach(PasswordBox box)
    {
        if ((bool)box.GetValue(AttachedProperty)) return;

        box.SetValue(AttachedProperty, true);
        box.PasswordChanged += OnPasswordChanged;
        box.Loaded += (_, _) => OnPasswordChanged(box, new RoutedEventArgs());
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox box) return;

        box.SetValue(UpdatingProperty, true);
        SetBoundPassword(box, box.Password);
        box.SetValue(UpdatingProperty, false);
    }
}
