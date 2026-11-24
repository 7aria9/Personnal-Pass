using System.Windows;
using System.Windows.Controls;

namespace PersonnalPass.App.Views;

/// <summary>Champ mot de passe avec un bouton « œil » pour l'afficher en clair.</summary>
public partial class RevealPasswordBox : UserControl
{
    private bool _syncing;

    public RevealPasswordBox() => InitializeComponent();

    public event EventHandler? PasswordChanged;

    public string Password
    {
        get => Toggle.IsChecked == true ? Plain.Text : Hidden.Password;
        set
        {
            _syncing = true;
            Hidden.Password = value;
            Plain.Text = value;
            _syncing = false;
            PasswordChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsRevealed
    {
        get => Toggle.IsChecked == true;
        set => Toggle.IsChecked = value;
    }

    public void FocusInput()
    {
        if (IsRevealed) Plain.Focus(); else Hidden.Focus();
    }

    private void Toggle_Changed(object sender, RoutedEventArgs e)
    {
        var reveal = Toggle.IsChecked == true;
        Plain.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;
        Hidden.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Hidden_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _syncing = true;
        Plain.Text = Hidden.Password;
        _syncing = false;
        PasswordChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Plain_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        _syncing = true;
        Hidden.Password = Plain.Text;
        _syncing = false;
        PasswordChanged?.Invoke(this, EventArgs.Empty);
    }
}
