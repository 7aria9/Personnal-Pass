using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using PersonnalPass.App.Services;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App.Views;

public partial class MainWindow : Window
{
    /// <summary>Verrouillage automatique après cette durée sans activité.</summary>
    private static readonly TimeSpan IdleLock = TimeSpan.FromMinutes(5);

    private readonly VaultSession _session;
    private readonly ObservableCollection<VaultEntry> _items = [];
    private readonly ICollectionView _view;
    private readonly DispatcherTimer _idleTimer;
    private bool _locking;

    public MainWindow(VaultSession session)
    {
        InitializeComponent();
        _session = session;

        _view = CollectionViewSource.GetDefaultView(_items);
        _view.SortDescriptions.Add(new SortDescription(nameof(VaultEntry.Name), ListSortDirection.Ascending));
        _view.Filter = Matches;
        EntriesGrid.ItemsSource = _view;

        _idleTimer = new DispatcherTimer { Interval = IdleLock };
        _idleTimer.Tick += (_, _) => Lock(automatic: true);
        _idleTimer.Start();
        PreviewMouseMove += (_, _) => ResetIdle();
        PreviewKeyDown += (_, _) => ResetIdle();
        SystemEvents.SessionSwitch += OnSessionSwitch;

        InputBindings.Add(new KeyBinding(new RelayCommand(() => Lock(automatic: false)), Key.L, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => Add_Click(this, new RoutedEventArgs())), Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => SearchBox.Focus()), Key.F, ModifierKeys.Control));

        Reload();
    }

    // ------------------------------------------------------------------ liste

    private void Reload(Guid? select = null)
    {
        _items.Clear();
        foreach (var entry in _session.Entries) _items.Add(entry);
        if (select is { } id) EntriesGrid.SelectedItem = _items.FirstOrDefault(e => e.Id == id);
        UpdateState();
    }

    private bool Matches(object item)
    {
        var text = SearchBox.Text.Trim();
        if (text.Length == 0) return true;
        var e = (VaultEntry)item;
        return e.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || e.Login.Contains(text, StringComparison.CurrentCultureIgnoreCase);
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _view.Refresh();
        UpdateState();
    }

    private void UpdateState()
    {
        var visible = _view.Cast<object>().Count();
        EmptyState.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _items.Count == 0
            ? "Aucun enregistrement pour l'instant. Cliquez sur « Ajouter »."
            : "Aucun résultat pour cette recherche.";
        StatusText.Text = _items.Count switch
        {
            0 => "Coffre vide",
            1 => "1 enregistrement",
            var n => $"{n} enregistrements",
        };
    }

    // ------------------------------------------------------------------ actions

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EntryWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var entry = _session.Add(dialog.EntryName, dialog.EntryLogin, dialog.EntryPassword);
        Reload(entry.Id);
    }

    private void Edit_Click(object sender, RoutedEventArgs e) => Edit(EntryFrom(sender));

    private void Grid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(EntriesGrid, source) is DataGridRow row)
            Edit(row.Item as VaultEntry);
    }

    private void Grid_KeyDown(object sender, KeyEventArgs e)
    {
        if (EntriesGrid.SelectedItem is not VaultEntry entry) return;
        switch (e.Key)
        {
            case Key.Enter:
                Edit(entry);
                e.Handled = true;
                break;
            case Key.Delete:
                Delete(entry);
                e.Handled = true;
                break;
            case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                Copy(entry.Password, "Mot de passe");
                e.Handled = true;
                break;
            case Key.B when Keyboard.Modifiers == ModifierKeys.Control:
                Copy(entry.Login, "Identifiant");
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Clic droit sur une cellule « Identifiant » ou « Mot de passe » : copie directe de la valeur,
    /// comme dans l'ancienne version WinDev.
    /// </summary>
    private void Grid_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var cell = FindParent<DataGridCell>(source);
        if (cell?.DataContext is not VaultEntry entry) return;

        if (cell.Column == LoginColumn)
            Copy(entry.Login, "Identifiant");
        else if (cell.Column == PasswordColumn)
            Copy(entry.Password, "Mot de passe");
        else
            return;

        EntriesGrid.SelectedItem = entry;
        e.Handled = true;
    }

    private static T? FindParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null and not T)
            element = element is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        return element as T;
    }

    private void Edit(VaultEntry? entry)
    {
        if (entry is null) return;
        var dialog = new EntryWindow(entry) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        var updated = entry.Clone();
        updated.Name = dialog.EntryName;
        updated.Login = dialog.EntryLogin;
        updated.Password = dialog.EntryPassword;
        _session.Update(updated);
        Reload(entry.Id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e) => Delete(EntryFrom(sender));

    private void Delete(VaultEntry? entry)
    {
        if (entry is null) return;
        var answer = MessageBox.Show(this, $"Supprimer définitivement « {entry.Name} » ?", "Supprimer",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;
        _session.Remove(entry.Id);
        Reload();
    }

    private void CopyLogin_Click(object sender, RoutedEventArgs e) => Copy(EntryFrom(sender)?.Login, "Identifiant");
    private void CopyPassword_Click(object sender, RoutedEventArgs e) => Copy(EntryFrom(sender)?.Password, "Mot de passe");

    private void Copy(string? value, string what)
    {
        if (string.IsNullOrEmpty(value)) return;
        var isPassword = what == "Mot de passe";
        if (!SecureClipboard.Copy(value, autoClear: isPassword))
            StatusText.Text = "Le presse-papiers est occupé, réessayez.";
        else
            StatusText.Text = isPassword
                ? $"Mot de passe copié. Le presse-papiers sera vidé dans {SecureClipboard.ClearDelay.TotalSeconds:0} secondes."
                : $"{what} copié dans le presse-papiers.";
    }

    private static VaultEntry? EntryFrom(object sender) => (sender as FrameworkElement)?.DataContext as VaultEntry;

    // ------------------------------------------------------------------ sécurité

    private void Security_Click(object sender, RoutedEventArgs e)
    {
        var menu = SecurityButton.ContextMenu;
        menu.PlacementTarget = SecurityButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void Lock_Click(object sender, RoutedEventArgs e) => Lock(automatic: false);

    private void ChangeCredentials_Click(object sender, RoutedEventArgs e)
    {
        if (ChangeCredentialsWindow.Prompt(this, _session))
            StatusText.Text = "Identifiants du coffre modifiés.";
    }

    private void NewRecoveryKit_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            "Un nouveau fichier et un nouveau code de récupération vont être générés.\n\n" +
            "L'ancien kit reste valable : détruisez-le si vous pensez qu'il a été compromis.\n\nContinuer ?",
            "Nouveau kit de récupération", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        RecoveryKitWindow.ShowKit(this, _session.CreateRecoveryKit(), firstTime: false);
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Sauvegarder le coffre",
            Filter = "Coffre Personnal-Pass (*.ppv)|*.ppv",
            FileName = $"PersonnalPass-{DateTime.Now:yyyy-MM-dd}.ppv",
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.Copy(_session.FilePath, dialog.FileName, overwrite: true);
            StatusText.Text = "Sauvegarde enregistrée (toujours chiffrée).";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Sauvegarde impossible", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetIdle()
    {
        _idleTimer.Stop();
        _idleTimer.Start();
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
            Dispatcher.BeginInvoke(() => Lock(automatic: true));
    }

    private void Lock(bool automatic)
    {
        if (_locking) return;
        // Une fenêtre de saisie est ouverte : on ne la ferme pas brutalement, on retentera plus tard.
        if (automatic && OwnedWindows.Count > 0)
        {
            ResetIdle();
            return;
        }
        _locking = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _idleTimer.Stop();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SecureClipboard.ClearIfOurs();
        _items.Clear();
        _session.Dispose();
        base.OnClosed(e);

        if (_locking) App.Current.ShowUnlockFlow();
        else App.Current.Shutdown();
    }
}
