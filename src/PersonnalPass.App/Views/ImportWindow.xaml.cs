using System.Windows;
using Microsoft.Win32;
using PersonnalPass.App.Services;
using PersonnalPass.Core.Import;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.App.Views;

/// <summary>Import d'enregistrements depuis un CSV, avec aperçu avant de les ajouter au coffre.</summary>
public partial class ImportWindow : Window
{
    private readonly VaultSession _session;
    private CsvImportResult? _result;
    private List<PreviewRow> _preview = [];

    public int ImportedCount { get; private set; }

    private ImportWindow(VaultSession session)
    {
        InitializeComponent();
        _session = session;
    }

    /// <returns>Le nombre d'enregistrements importés (0 si annulé).</returns>
    public static int Prompt(Window owner, VaultSession session)
    {
        var window = new ImportWindow(session) { Owner = owner };
        return window.ShowDialog() == true ? window.ImportedCount : 0;
    }

    public sealed record PreviewRow(int Line, string Name, string Login, string Password, string Status, bool Importable)
    {
        public string DisplayedPassword { get; init; } = "";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choisir le fichier CSV à importer",
            Filter = "Fichiers CSV (*.csv;*.txt)|*.csv;*.txt|Tous les fichiers (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != true) return;
        FileBox.Text = dialog.FileName;
        Load(hasHeader: null);
    }

    private void Header_Click(object sender, RoutedEventArgs e) => Load(HeaderCheck.IsChecked == true);

    private void Options_Click(object sender, RoutedEventArgs e) => BuildPreview();

    private void Load(bool? hasHeader)
    {
        try
        {
            _result = CsvImporter.ReadFile(FileBox.Text, hasHeader);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialog.Show(this, ex.Message, "Lecture impossible", MessageBoxButton.OK, MessageBoxImage.Error);
            _result = null;
        }

        HeaderCheck.IsEnabled = _result is not null;
        HeaderCheck.IsChecked = _result?.HasHeader == true;
        DetectedText.Text = _result is null ? "" :
            $"Séparateur : {DelimiterName(_result.Delimiter)}   ·   Encodage : {_result.EncodingName}   ·   {_result.Rows.Count} ligne(s) de données";
        BuildPreview();
    }

    private void BuildPreview()
    {
        _preview = [];
        if (_result is not null)
        {
            var skipDuplicates = SkipDuplicatesCheck.IsChecked == true;
            var show = ShowPasswordsCheck.IsChecked == true;
            var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

            foreach (var row in _result.Rows)
            {
                string status;
                var importable = false;
                var key = row.Name.Trim() + "\u0001" + row.Login.Trim();

                if (!row.IsValid) status = row.Error!;
                else if (skipDuplicates && _session.Contains(row.Name, row.Login)) status = "Déjà dans le coffre";
                else if (skipDuplicates && !seen.Add(key)) status = "En double dans le fichier";
                else { status = "À importer"; importable = true; }

                _preview.Add(new PreviewRow(row.LineNumber, row.Name, row.Login, row.Password, status, importable)
                {
                    DisplayedPassword = show ? row.Password : (row.Password.Length > 0 ? "••••••••" : ""),
                });
            }
        }

        PreviewGrid.ItemsSource = _preview;
        var toImport = _preview.Count(r => r.Importable);
        var ignored = _preview.Count - toImport;
        ImportButton.IsEnabled = toImport > 0;
        ImportButton.Content = toImport > 0 ? $"Importer {toImport}" : "Importer";
        SummaryText.Text = _result is null ? "" :
            $"{toImport} enregistrement(s) à importer" + (ignored > 0 ? $", {ignored} ignoré(s)." : ".");
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var rows = _preview.Where(r => r.Importable).Select(r => (r.Name, r.Login, r.Password)).ToList();
        ImportedCount = _session.AddRange(rows);

        var answer = Dialog.Show(this,
            $"{ImportedCount} enregistrement(s) importé(s) et chiffré(s) dans le coffre.\n\n" +
            "Le fichier CSV contient toujours vos mots de passe en clair. Voulez-vous le supprimer maintenant ?",
            "Import terminé", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            try
            {
                File.Delete(FileBox.Text);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Dialog.Show(this, ex.Message, "Suppression impossible", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        DialogResult = true;
    }

    private static string DelimiterName(char c) => c switch
    {
        ';' => "point-virgule",
        ',' => "virgule",
        '\t' => "tabulation",
        _ => $"« {c} »",
    };
}
