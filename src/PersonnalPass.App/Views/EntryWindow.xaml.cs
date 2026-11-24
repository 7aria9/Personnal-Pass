using System.Windows;
using PersonnalPass.Core;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App.Views;

/// <summary>Ajout ou modification d'un enregistrement, avec générateur de mot de passe.</summary>
public partial class EntryWindow : Window
{
    public string EntryName => NameBox.Text.Trim();
    public string EntryLogin => LoginBox.Text;
    public string EntryPassword => PasswordField.Password;

    public EntryWindow(VaultEntry? existing = null)
    {
        InitializeComponent();
        LengthSlider.Minimum = PasswordGenerator.MinLength;
        LengthSlider.Maximum = PasswordGenerator.MaxLength;

        if (existing is not null)
        {
            Title = Heading.Text = "Modifier l'enregistrement";
            NameBox.Text = existing.Name;
            LoginBox.Text = existing.Login;
            PasswordField.Password = existing.Password;
        }
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        var options = new PasswordOptions(
            (int)LengthSlider.Value,
            UpperCheck.IsChecked == true,
            LowerCheck.IsChecked == true,
            DigitCheck.IsChecked == true,
            SpecialCheck.IsChecked == true);

        if (!options.HasAnyCharacterSet)
        {
            ShowError("Cochez au moins un type de caractères.");
            return;
        }

        ErrorText.Visibility = Visibility.Collapsed;
        PasswordField.Password = PasswordGenerator.Generate(options);
        PasswordField.IsRevealed = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (EntryName.Length == 0)
        {
            ShowError("Le nom est obligatoire.");
            NameBox.Focus();
            return;
        }
        if (EntryPassword.Length == 0)
        {
            ShowError("Saisissez ou générez un mot de passe.");
            PasswordField.FocusInput();
            return;
        }
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
