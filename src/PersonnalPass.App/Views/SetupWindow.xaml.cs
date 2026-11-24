using System.Windows;
using Microsoft.Win32;
using PersonnalPass.App.Services;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App.Views;

public partial class SetupWindow : Window
{
    public VaultSession? Session { get; private set; }

    public SetupWindow() => InitializeComponent();

    public static VaultSession? Prompt()
    {
        var window = new SetupWindow();
        return window.ShowDialog() == true ? window.Session : null;
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var error = CredentialRules.Validate(LoginBox.Text, PasswordBox.Password, ConfirmBox.Password);
        if (error is not null)
        {
            ErrorText.Text = error;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        CreateButton.IsEnabled = false;
        Busy.Visibility = Visibility.Visible;
        try
        {
            var (login, password, hardware) = (LoginBox.Text.Trim(), PasswordBox.Password, App.Current.Hardware);
            var (session, kit) = await Task.Run(() => VaultSession.Create(AppPaths.VaultFile, login, password, hardware));

            // Le kit de récupération doit être sauvegardé avant d'utiliser le coffre.
            RecoveryKitWindow.ShowKit(this, kit, firstTime: true);
            Session = session;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            CreateButton.IsEnabled = true;
            Busy.Visibility = Visibility.Hidden;
        }
    }

    /// <summary>
    /// Nouvel ordinateur : on copie la sauvegarde du coffre (vault.ppv) puis on l'ouvre
    /// avec le fichier et le code de récupération.
    /// </summary>
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choisir la sauvegarde du coffre",
            Filter = "Coffre Personnal-Pass (*.ppv)|*.ppv|Tous les fichiers (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            Directory.CreateDirectory(AppPaths.DataFolder);
            File.Copy(dialog.FileName, AppPaths.VaultFile, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        var session = RecoveryWindow.Prompt(this);
        if (session is null)
        {
            File.Delete(AppPaths.VaultFile); // import annulé : on revient à l'état initial
            return;
        }
        Session = session;
        DialogResult = true;
    }
}
