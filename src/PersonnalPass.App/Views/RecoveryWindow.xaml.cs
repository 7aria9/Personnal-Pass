using System.Windows;
using Microsoft.Win32;
using PersonnalPass.App.Services;
using PersonnalPass.Core.Crypto;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App.Views;

public partial class RecoveryWindow : Window
{
    public VaultSession? Session { get; private set; }

    public RecoveryWindow() => InitializeComponent();

    public static VaultSession? Prompt(Window owner)
    {
        var window = new RecoveryWindow { Owner = owner };
        return window.ShowDialog() == true ? window.Session : null;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Choisir le fichier de récupération", Filter = AppPaths.RecoveryFilter };
        if (dialog.ShowDialog(this) == true) FileBox.Text = dialog.FileName;
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        string? error = null;
        if (string.IsNullOrEmpty(FileBox.Text) || !File.Exists(FileBox.Text))
            error = "Choisissez le fichier de récupération.";
        else if (!RecoveryCode.LooksValid(CodeBox.Text))
            error = "Le code de récupération doit comporter 25 caractères (ex. ABCDE-FGHJK-…).";
        else
            error = CredentialRules.Validate(LoginBox.Text, PasswordBox.Password, ConfirmBox.Password);

        if (error is not null)
        {
            ShowError(error);
            return;
        }

        RestoreButton.IsEnabled = false;
        Busy.Visibility = Visibility.Visible;
        try
        {
            var content = await File.ReadAllTextAsync(FileBox.Text);
            var (code, login, password, hardware) = (CodeBox.Text, LoginBox.Text.Trim(), PasswordBox.Password, App.Current.Hardware);
            Session = await Task.Run(() => VaultSession.Recover(AppPaths.VaultFile, content, code, login, password, hardware));
            MessageBox.Show(this, "Le coffre est de nouveau accessible avec vos nouveaux identifiants sur cet ordinateur.",
                "Récupération réussie", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex) when (ex is VaultException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ShowError(ex.Message);
        }
        finally
        {
            RestoreButton.IsEnabled = true;
            Busy.Visibility = Visibility.Hidden;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
