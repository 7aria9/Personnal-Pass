using System.Text;

namespace PersonnalPass.Core.Import;

/// <summary>Une ligne lue dans le CSV, prête à être importée (ou en erreur).</summary>
public sealed record ImportRow(int LineNumber, string Name, string Login, string Password, string? Error)
{
    public bool IsValid => Error is null;
}

/// <summary>Résultat de la lecture : lignes + ce qui a été détecté, pour l'afficher à l'utilisateur.</summary>
public sealed record CsvImportResult(
    IReadOnlyList<ImportRow> Rows,
    char Delimiter,
    string EncodingName,
    bool HasHeader,
    int NameColumn,
    int LoginColumn,
    int PasswordColumn);

/// <summary>
/// Lecture d'un CSV « Nom ; Identifiant ; Mot de passe » (export WinDev, Excel, autre gestionnaire…).
/// <list type="bullet">
/// <item>séparateur détecté automatiquement : point-virgule, virgule ou tabulation ;</item>
/// <item>encodage : UTF-8 (avec ou sans BOM), sinon Windows-1252 (ANSI, cas typique des exports WinDev/Excel) ;</item>
/// <item>ligne d'en-têtes reconnue (nom / login / mot de passe…) pour remettre les colonnes dans l'ordre ;
/// sans en-têtes, l'ordre Nom, Identifiant, Mot de passe est utilisé ;</item>
/// <item>champs entre guillemets (avec "" pour un guillemet, et retours à la ligne) gérés.</item>
/// </list>
/// </summary>
public static class CsvImporter
{
    private static readonly string[] NameHeaders = ["nom", "name", "titre", "title", "site", "libelle", "libellé", "service", "compte", "account"];
    private static readonly string[] LoginHeaders = ["login", "identifiant", "id", "user", "username", "utilisateur", "user name", "email", "e-mail", "mail"];
    private static readonly string[] PasswordHeaders = ["mot de passe", "motdepasse", "mot_de_passe", "mdp", "password", "pass", "pwd", "passwd"];

    /// <param name="hasHeader">null = détection automatique ; true/false pour forcer.</param>
    public static CsvImportResult ReadFile(string path, bool? hasHeader = null) => Parse(File.ReadAllBytes(path), hasHeader);

    public static CsvImportResult Parse(byte[] content, bool? hasHeader = null)
    {
        var (text, encodingName) = Decode(content);
        return Parse(text, encodingName, hasHeader);
    }

    public static CsvImportResult Parse(string text, string encodingName = "texte", bool? forceHeader = null)
    {
        var delimiter = DetectDelimiter(text);
        var records = SplitRecords(text, delimiter);

        // Les lignes entièrement vides sont ignorées.
        records.RemoveAll(r => r.Fields.All(string.IsNullOrWhiteSpace));

        int nameCol = 0, loginCol = 1, passwordCol = 2;
        var hasHeader = false;
        if (records.Count > 0 && forceHeader != false)
        {
            if (TryMapHeader(records[0].Fields, out var n, out var l, out var p))
            {
                (nameCol, loginCol, passwordCol) = (n, l, p);
                hasHeader = true;
            }
            else hasHeader = forceHeader == true;
            if (hasHeader) records.RemoveAt(0);
        }

        var rows = records.Select(r => ToRow(r, nameCol, loginCol, passwordCol)).ToList();
        return new CsvImportResult(rows, delimiter, encodingName, hasHeader, nameCol, loginCol, passwordCol);
    }

    // ------------------------------------------------------------------ encodage

    internal static (string Text, string EncodingName) Decode(byte[] content)
    {
        // BOM UTF-8 / UTF-16
        if (content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF)
            return (Encoding.UTF8.GetString(content, 3, content.Length - 3), "UTF-8");
        if (content.Length >= 2 && content[0] == 0xFF && content[1] == 0xFE)
            return (Encoding.Unicode.GetString(content, 2, content.Length - 2), "UTF-16");
        if (content.Length >= 2 && content[0] == 0xFE && content[1] == 0xFF)
            return (Encoding.BigEndianUnicode.GetString(content, 2, content.Length - 2), "UTF-16 BE");

        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return (strictUtf8.GetString(content), "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return (Encoding.GetEncoding(1252).GetString(content), "Windows-1252 (ANSI)");
        }
    }

    // ------------------------------------------------------------------ séparateur

    internal static char DetectDelimiter(string text)
    {
        // On compte les séparateurs hors guillemets sur les premières lignes.
        char[] candidates = [';', ',', '\t'];
        var counts = new int[candidates.Length];
        var inQuotes = false;
        var lines = 0;
        foreach (var c in text)
        {
            if (c == '"') inQuotes = !inQuotes;
            else if (!inQuotes && c == '\n' && ++lines >= 20) break;
            else if (!inQuotes)
                for (var i = 0; i < candidates.Length; i++)
                    if (c == candidates[i]) counts[i]++;
        }
        var best = 0;
        for (var i = 1; i < candidates.Length; i++)
            if (counts[i] > counts[best]) best = i;
        return counts[best] == 0 ? ';' : candidates[best];
    }

    // ------------------------------------------------------------------ découpage

    private sealed record Record(int LineNumber, List<string> Fields);

    private static List<Record> SplitRecords(string text, char delimiter)
    {
        var records = new List<Record>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordStart = 1;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }
                continue;
            }

            if (c == '"' && field.Length == 0) inQuotes = true;
            else if (c == delimiter) { fields.Add(field.ToString()); field.Clear(); }
            else if (c == '\r') { /* ignoré : \r\n géré par \n */ }
            else if (c == '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                records.Add(new Record(recordStart, fields));
                fields = [];
                line++;
                recordStart = line;
            }
            else field.Append(c);
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(new Record(recordStart, fields));
        }
        return records;
    }

    // ------------------------------------------------------------------ colonnes

    private static bool TryMapHeader(List<string> header, out int name, out int login, out int password)
    {
        name = login = password = -1;
        for (var i = 0; i < header.Count; i++)
        {
            var h = header[i].Trim().ToLowerInvariant();
            if (password < 0 && PasswordHeaders.Contains(h)) password = i;
            else if (login < 0 && LoginHeaders.Contains(h)) login = i;
            else if (name < 0 && NameHeaders.Contains(h)) name = i;
        }
        // Il faut reconnaître au moins deux colonnes pour considérer la ligne comme un en-tête.
        var recognised = (name >= 0 ? 1 : 0) + (login >= 0 ? 1 : 0) + (password >= 0 ? 1 : 0);
        if (recognised < 2) return false;

        // Colonne manquante : on prend la première colonne libre.
        var used = new HashSet<int> { name, login, password };
        int NextFree() { var j = 0; while (used.Contains(j)) j++; used.Add(j); return j; }
        if (name < 0) name = NextFree();
        if (login < 0) login = NextFree();
        if (password < 0) password = NextFree();
        return true;
    }

    private static ImportRow ToRow(Record record, int nameCol, int loginCol, int passwordCol)
    {
        string Get(int col) => col < record.Fields.Count ? record.Fields[col] : "";
        var name = Get(nameCol).Trim();
        var login = Get(loginCol).Trim();
        var password = Get(passwordCol); // pas de Trim : un espace peut faire partie du mot de passe

        string? error = null;
        if (name.Length == 0) error = "Nom manquant";
        else if (password.Length == 0) error = "Mot de passe manquant";
        return new ImportRow(record.LineNumber, name, login, password, error);
    }
}
