using System.Windows;
using System.Windows.Threading;
using PersonnalPass.App.Services;
using PersonnalPass.App.Views;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App;

public partial class App : Application
{
    public IHardwareFingerprintProvider Hardware { get; } = new ProcessorFingerprint();

    public static new App Current => (App)Application.Current;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;
        ShowUnlockFlow();
    }

    /// <summary>Affiche la connexion (ou la création du coffre au premier lancement), puis la fenêtre principale.</summary>
    public void ShowUnlockFlow()
    {
        VaultSession? session;
        try
        {
            session = VaultSession.Exists(AppPaths.VaultFile)
                ? LoginWindow.Prompt()
                : SetupWindow.Prompt();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Personnal-Pass", MessageBoxButton.OK, MessageBoxImage.Error);
            session = null;
        }

        if (session is null)
        {
            Shutdown();
            return;
        }

        var main = new MainWindow(session);
        MainWindow = main;
        main.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SecureClipboard.ClearIfOurs();
        base.OnExit(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "Erreur inattendue", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
