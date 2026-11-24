using System.Text;
using PersonnalPass.Core.Import;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.Core.Tests;

public class CsvImporterTests
{
    [Fact]
    public void Semicolon_without_header_uses_default_order()
    {
        var r = CsvImporter.Parse("Banque;david;s3cr3t\r\nMail;moi@x.fr;abc;def\r\n");
        Assert.Equal(';', r.Delimiter);
        Assert.False(r.HasHeader);
        Assert.Equal(2, r.Rows.Count);
        Assert.Equal(("Banque", "david", "s3cr3t"), (r.Rows[0].Name, r.Rows[0].Login, r.Rows[0].Password));
        Assert.Equal("abc", r.Rows[1].Password);
    }

    [Fact]
    public void Header_reorders_columns_and_is_skipped()
    {
        var r = CsvImporter.Parse("Password,Username,Name,URL\npw1,user1,Site1,http://a\n");
        Assert.Equal(',', r.Delimiter);
        Assert.True(r.HasHeader);
        var row = Assert.Single(r.Rows);
        Assert.Equal(("Site1", "user1", "pw1"), (row.Name, row.Login, row.Password));
    }

    [Fact]
    public void French_header_is_recognised()
    {
        var r = CsvImporter.Parse("Nom;Identifiant;Mot de passe\nImpôts;123;x\n");
        Assert.True(r.HasHeader);
        Assert.Equal("Impôts", Assert.Single(r.Rows).Name);
    }

    [Fact]
    public void Quoted_fields_with_delimiters_quotes_and_newlines()
    {
        var r = CsvImporter.Parse("\"Site; avec point-virgule\";\"lo\"\"gin\";\"mot\nde passe\"\nB;c;d\n");
        Assert.Equal(2, r.Rows.Count);
        Assert.Equal("Site; avec point-virgule", r.Rows[0].Name);
        Assert.Equal("lo\"gin", r.Rows[0].Login);
        Assert.Equal("mot\nde passe", r.Rows[0].Password);
        Assert.Equal(3, r.Rows[1].LineNumber);
    }

    [Fact]
    public void Tab_delimiter()
    {
        Assert.Equal('\t', CsvImporter.Parse("a\tb\tc\n").Delimiter);
    }

    [Fact]
    public void Missing_values_are_flagged_and_blank_lines_ignored()
    {
        var r = CsvImporter.Parse("A;b;\n\n;c;d\nE;;f\n");
        Assert.Equal(3, r.Rows.Count);
        Assert.Equal("Mot de passe manquant", r.Rows[0].Error);
        Assert.Equal("Nom manquant", r.Rows[1].Error);
        Assert.True(r.Rows[2].IsValid); // identifiant vide autorisé
    }

    [Fact]
    public void Password_spaces_are_kept()
    {
        Assert.Equal(" pw ", Assert.Single(CsvImporter.Parse("A;b; pw \n").Rows).Password);
    }

    [Fact]
    public void Windows1252_file_is_decoded()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(1252).GetBytes("Sécurité sociale;josé;mdpé€\r\n");
        var r = CsvImporter.Parse(bytes);
        Assert.StartsWith("Windows-1252", r.EncodingName);
        var row = Assert.Single(r.Rows);
        Assert.Equal(("Sécurité sociale", "josé", "mdpé€"), (row.Name, row.Login, row.Password));
    }

    [Fact]
    public void Utf8_with_bom_is_decoded()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Nom;Login;MDP\nÉcole;é;€\n")).ToArray();
        var r = CsvImporter.Parse(bytes);
        Assert.Equal("UTF-8", r.EncodingName);
        Assert.Equal("École", Assert.Single(r.Rows).Name);
    }

    [Fact]
    public void AddRange_saves_once_and_detects_duplicates()
    {
        using var dir = new TempDir();
        var path = dir.File("vault.ppv");
        var hw = new FakeHardware("CPU-A");
        var (session, _) = VaultSession.Create(path, "David", "MotDePasse!123", hw, 1_000, 1_000);
        using (session)
        {
            session.Add("Banque", "david", "x");
            Assert.True(session.Contains(" banque ", "DAVID"));
            Assert.False(session.Contains("Banque", "autre"));
            Assert.Equal(2, session.AddRange([("A", "a", "1"), ("B", "b", "2")]));
        }
        using var reopened = VaultSession.Unlock(path, "David", "MotDePasse!123", hw);
        Assert.Equal(3, reopened.Entries.Count);
    }
}

public class CsvImporterHeaderOverrideTests
{
    [Fact]
    public void Header_detection_can_be_forced_off_or_on()
    {
        const string csv = "Site;User;Pass\nA;b;c\n";
        Assert.True(CsvImporter.Parse(csv).HasHeader);
        Assert.Equal(2, CsvImporter.Parse(csv, forceHeader: false).Rows.Count);
        Assert.Single(CsvImporter.Parse("X;y;z\nA;b;c\n", forceHeader: true).Rows);
    }
}
