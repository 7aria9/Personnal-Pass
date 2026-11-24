using System.Windows;
using PersonnalPass.App.Services;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App.Views;

public partial class LoginWindow : Window
{
    private int _failures;

    public VaultSession? Session { get; private set; }

    public LoginWindow() => InitializeComponent();

    public static VaultSession? Prompt()
    {
        var window = new LoginWindow();
        return window.ShowDialog() == true ? window.Session : null;
    }

    private async void Unlock_Click(object sender, RoutedEventArgs e)
    {
        var login = LoginBox.Text;
        var password = PasswordBox.Password;
        if (string.IsNullOrWhiteSpace(login) || password.Length == 0)
        {
            ShowError("Saisissez votre identifiant et votre mot de passe.");
            return;
        }

        SetBusy(true);
        try
        {
            // La dérivation de clé est volontairement lente (~0,5 s) : on ne bloque pas l'interface.
            var hardware = App.Current.Hardware;
            Session = await Task.Run(() => VaultSession.Unlock(AppPaths.VaultFile, login, password, hardware));
            DialogResult = true;
        }
        catch (VaultException ex) when (ex.Error == VaultError.HardwareChanged)
        {
            ShowError(ex.Message);
            RecoveryLink.FontWeight = FontWeights.Bold;
        }
        catch (VaultException ex)
        {
            _failures++;
            ShowError(ex.Message);
            PasswordBox.Clear();
            PasswordBox.Focus();
            // Petit délai croissant pour freiner les essais à répétition.
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Min(_failures * 500, 5000)));
        }
        catch (InvalidOperationException ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Recovery_Click(object sender, RoutedEventArgs e)
    {
        var session = RecoveryWindow.Prompt(this);
        if (session is null) return;
        Session = session;
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void SetBusy(bool busy)
    {
        if (!IsLoaded) return;
        UnlockButton.IsEnabled = !busy;
        Busy.Visibility = busy ? Visibility.Visible : Visibility.Hidden;
    }
}
