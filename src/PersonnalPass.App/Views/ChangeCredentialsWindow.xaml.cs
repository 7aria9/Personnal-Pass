using System.Windows;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App.Views;

public partial class ChangeCredentialsWindow : Window
{
    private readonly VaultSession _session;

    private ChangeCredentialsWindow(VaultSession session)
    {
        InitializeComponent();
        _session = session;
    }

    public static bool Prompt(Window owner, VaultSession session) =>
        new ChangeCredentialsWindow(session) { Owner = owner }.ShowDialog() == true;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var error = CredentialRules.Validate(LoginBox.Text, PasswordBox.Password, ConfirmBox.Password);
        if (error is not null)
        {
            ShowError(error);
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var (path, currentLogin, currentPassword, hardware) =
                (_session.FilePath, CurrentLoginBox.Text, CurrentPasswordBox.Password, App.Current.Hardware);
            var (login, password) = (LoginBox.Text.Trim(), PasswordBox.Password);

            // On revérifie les identifiants actuels avant de les remplacer.
            await Task.Run(() => VaultSession.Unlock(path, currentLogin, currentPassword, hardware).Dispose());
            await Task.Run(() => _session.ChangeCredentials(login, password));
            DialogResult = true;
        }
        catch (VaultException ex)
        {
            ShowError(ex.Error == VaultError.WrongCredentials ? "Identifiants actuels incorrects." : ex.Message);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
