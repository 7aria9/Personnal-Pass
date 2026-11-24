using PersonnalPass.Core.Crypto;
using PersonnalPass.Core.Vault;

namespace PersonnalPass.Core.Tests;

public class VaultSessionTests
{
    // Peu d'itérations pour garder des tests rapides.
    private const int Iter = 1_000;

    private static (VaultSession, RecoveryKit) NewVault(string path, FakeHardware hw) =>
        VaultSession.Create(path, "David", "MotDePasse!123", hw, Iter, Iter);

    [Fact]
    public void Create_add_reopen_roundtrip()
    {
        using var dir = new TempDir();
        var path = dir.File("vault.ppv");
        var hw = new FakeHardware("CPU-A");

        var (session, _) = NewVault(path, hw);
        using (session)
        {
            session.Add("Banque", "david@example.com", "s3cr3t!");
            session.Add("Mail", "david", "autre");
        }

        // L'identifiant n'est pas sensible à la casse.
        using var reopened = VaultSession.Unlock(path, "david", "MotDePasse!123", hw);
        Assert.Equal(2, reopened.Entries.Count);
        Assert.Contains(reopened.Entries, e => e.Name == "Banque" && e.Password == "s3cr3t!");
    }

    [Fact]
    public void File_does_not_contain_plaintext()
    {
        using var dir = new TempDir();
        var path = dir.File("vault.ppv");
        var (session, kit) = NewVault(path, new FakeHardware("CPU-A"));
        using (session) session.Add("MaBanque", "identifiant-secret", "motdepasse-secret");

        var content = File.ReadAllText(path);
        Assert.DoesNotContain("MaBanque", content);
        Assert.DoesNotContain("identifiant-secret", content);
        Assert.DoesNotContain("motdepasse-secret", content);
        Assert.DoesNotContain("David", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RecoveryCode.Normalize(kit.RecoveryCode), kit.RecoveryFileContent);
    }

    [Fact]
    public void Update_and_remove()
    {
        using var dir = new TempDir();
        var path = dir.File("vault.ppv");
        var hw = new FakeHardware("CPU-A");
        var (session, _) = NewVault(path, hw);
        Guid id;
        using (session)
        {
            var e = session.Add("Site", "a", "b");
            var other = session.Add("Autre", "c", "d");
            id = e.Id;
            var edit = e.Clone();
            edit.Password = "nouveau";
            session.Update(edit);
            Assert.True(session.Remove(other.Id));
        }

        using var reopened = VaultSession.Unlock(path, "David", "MotDePasse!123", hw);
        var single = Assert.Single(reopened.Entries);
        Assert.Equal(id, single.Id);
        Assert.Equal("nouveau", single.Password);
    }

    [Fact]
    public void Wrong_password_is_reported_as_wrong_credentials()
    {
        using var dir = new TempDir();
        var path = dir.File("vault.ppv");
        var hw = new FakeHardware("CPU-A");
        NewVault(path, hw).Item1.Dispose();

        var ex = Assert.Throws<VaultException>(() => VaultSession.Unlock(path, "David", "mauvais-mdp", hw));
        Assert.Equal(VaultError.WrongCredentials, ex.Error);
        ex = Assert.Throws<VaultException>(() => VaultSession.Unlock(path, "Quelqu'un", "MotDePasse!123", hw));
        Assert.Equal(VaultError.WrongCredentials, ex.Error);
    }

    [Fact]
    public void New_processor_is_detected_and_recovery_restores_access()
    {
        using var dir = new TempDir();
        var path = dir.File("vault.ppv");
        var oldHw = new FakeHardware("CPU-A");
        var (session, kit) = NewVault(path, oldHw);
        using (session) session.Add("Banque", "moi", "secret");

        var newHw = new FakeHardware("CPU-B");
        var ex = Assert.Throws<VaultException>(() => VaultSession.Unlock(path, "David", "MotDePasse!123", newHw));
        Assert.Equal(VaultError.HardwareChanged, ex.Error);

        // Saisie tolérante : minuscules, espaces au lieu des tirets.
        var typed = kit.RecoveryCode.ToLowerInvariant().Replace('-', ' ');
        using (var recovered = VaultSession.Recover(path, kit.RecoveryFileContent, typed, "David", "NouveauMdp!456", newHw))
            Assert.Equal("secret", Assert.Single(recovered.Entries).Password);

        using var reopened = VaultSession.Unlock(path, "David", "NouveauMdp!456", newHw);
        Assert.Single(reopened.Entries);
        // L'ancien mot de passe ne fonctionne plus.
        Assert.Throws<VaultException>(() => VaultSession.Unlock(path, "David", "MotDePasse!123", newHw));
    }

    [Fact]
    public void Recovery_requires_the_right_code()
    {
        using var dir = new TempDir();
        var path = dir.File("vault.ppv");
        var (session, kit) = NewVault(path, new FakeHardware("CPU-A"));
        session.Dispose();

        var ex = Assert.Throws<VaultException>(() => VaultSession.Recover(
            path, kit.RecoveryFileContent, RecoveryCode.Generate(), "David", "NouveauMdp!456", new FakeHardware("CPU-B")));
        Assert.Equal(VaultError.WrongRecoveryCode, ex.Error);
    }

    [Fact]
    public void Recovery_file_from_another_vault_is_rejected()
    {
        using var dir = new TempDir();
        var hw = new FakeHardware("CPU-A");
        NewVault(dir.File("a.ppv"), hw).Item1.Dispose();
        var (other, otherKit) = NewVault(dir.File("b.ppv"), hw);
        other.Dispose();

        var ex = Assert.Throws<VaultException>(() => VaultSession.Recover(
            dir.File("a.ppv"), otherKit.RecoveryFileContent, otherKit.RecoveryCode, "David", "NouveauMdp!456", hw));
        Assert.Equal(VaultError.RecoveryFileMismatch, ex.Error);
    }

    [Fact]
    public void Tampered_vault_is_rejected()
    {
        using var dir = new TempDir();
        var path = dir.File("vault.ppv");
        var hw = new FakeHardware("CPU-A");
        var (session, _) = NewVault(path, hw);
        using (session) session.Add("x", "y", "z");

        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        var cipher = Convert.FromBase64String(json["data"]!["cipherText"]!.GetValue<string>());
        cipher[0] ^= 0xFF;
        json["data"]!["cipherText"] = Convert.ToBase64String(cipher);
        File.WriteAllText(path, json.ToJsonString());

        var ex = Assert.Throws<VaultException>(() => VaultSession.Unlock(path, "David", "MotDePasse!123", hw));
        Assert.Equal(VaultError.InvalidFile, ex.Error);
    }

    [Fact]
    public void Change_credentials()
    {
        using var dir = new TempDir();
        var path = dir.File("vault.ppv");
        var hw = new FakeHardware("CPU-A");
        var (session, _) = NewVault(path, hw);
        using (session) session.ChangeCredentials("Autre", "Different#789");

        using var s = VaultSession.Unlock(path, "Autre", "Different#789", hw);
        Assert.Empty(s.Entries);
    }

    [Fact]
    public void Recovery_code_format()
    {
        var code = RecoveryCode.Generate();
        Assert.Matches("^[A-Z2-9]{5}(-[A-Z2-9]{5}){4}$", code);
        Assert.True(RecoveryCode.LooksValid(code.ToLowerInvariant()));
        Assert.False(RecoveryCode.LooksValid("ABC"));
    }
}
