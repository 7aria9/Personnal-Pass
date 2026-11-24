using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
using PersonnalPass.App.Services;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App.Views;

public partial class RecoveryKitWindow : Window
{
    private readonly RecoveryKit _kit;
    private bool _fileSaved;
    private bool _done;

    private RecoveryKitWindow(RecoveryKit kit)
    {
        InitializeComponent();
        _kit = kit;
        CodeBox.Text = kit.RecoveryCode;
    }

    public static void ShowKit(Window owner, RecoveryKit kit, bool firstTime)
    {
        var window = new RecoveryKitWindow(kit) { Owner = owner };
        if (!firstTime) window.Title = "Personnal-Pass – Nouveau kit de récupération";
        window.ShowDialog();
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Enregistrer le fichier de récupération",
            Filter = AppPaths.RecoveryFilter,
            FileName = $"PersonnalPass-{DateTime.Now:yyyy-MM-dd}.pprecovery",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllText(dialog.FileName, _kit.RecoveryFileContent);
            _fileSaved = true;
            SavedText.Text = "✓ " + Path.GetFileName(dialog.FileName);
            SavedText.Visibility = Visibility.Visible;
            UpdateState();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Enregistrement impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Update_State(object sender, RoutedEventArgs e) => UpdateState();

    private void UpdateState() => DoneButton.IsEnabled = _fileSaved && ConfirmCheck.IsChecked == true;

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        _done = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_done)
        {
            var answer = MessageBox.Show(this,
                "Sans fichier ni code de récupération, un changement de processeur rendra le coffre définitivement illisible.\n\nFermer quand même ?",
                "Kit de récupération non sauvegardé", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            e.Cancel = answer != MessageBoxResult.Yes;
        }
        base.OnClosing(e);
    }
}
