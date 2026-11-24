using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PersonnalPass.Core.Crypto;

namespace PersonnalPass.Core.Vault;

/// <summary>Résultat de la création d'un coffre ou d'un nouveau fichier de récupération.</summary>
/// <param name="RecoveryCode">À noter sur papier : il n'est stocké nulle part.</param>
/// <param name="RecoveryFileContent">Contenu du fichier de récupération à enregistrer (clé USB, etc.).</param>
public sealed record RecoveryKit(string RecoveryCode, string RecoveryFileContent);

/// <summary>
/// Coffre déverrouillé. Schéma de chiffrement :
/// <list type="bullet">
/// <item>une clé de données aléatoire (256 bits) chiffre les enregistrements (AES-256-GCM) ;</item>
/// <item>cette clé est elle-même chiffrée par la clé maître = PBKDF2(identifiant + mot de passe) mélangée
/// à la signature du processeur (HKDF) ;</item>
/// <item>une seconde copie de la clé de données, chiffrée par le code de récupération,
/// est placée dans le fichier de récupération.</item>
/// </list>
/// </summary>
public sealed class VaultSession : IDisposable
{
    private const string KdfAlgorithm = "PBKDF2-SHA256+HKDF-SHA256(hardware)";
    private const string RecoveryKdfAlgorithm = "PBKDF2-SHA256";

    private readonly string _path;
    private readonly IHardwareFingerprintProvider _hardware;
    private readonly Guid _vaultId;
    private readonly int _iterations;
    private byte[] _dataKey;
    private VaultFileModel _header;
    private List<VaultEntry> _entries;
    private bool _disposed;

    private VaultSession(string path, IHardwareFingerprintProvider hardware, Guid vaultId, int iterations,
        byte[] dataKey, VaultFileModel header, List<VaultEntry> entries)
    {
        _path = path;
        _hardware = hardware;
        _vaultId = vaultId;
        _iterations = iterations;
        _dataKey = dataKey;
        _header = header;
        _entries = entries;
    }

    public string FilePath => _path;
    public IReadOnlyList<VaultEntry> Entries => _entries;

    public static bool Exists(string path) => File.Exists(path);

    // ---------------------------------------------------------------- création

    /// <summary>Crée un nouveau coffre vide et renvoie le kit de récupération à sauvegarder.</summary>
    public static (VaultSession Session, RecoveryKit Recovery) Create(
        string path, string login, string password, IHardwareFingerprintProvider hardware,
        int iterations = KeyDerivation.DefaultPasswordIterations,
        int recoveryIterations = KeyDerivation.DefaultRecoveryIterations)
    {
        ValidateCredentials(login, password);
        if (File.Exists(path)) throw new IOException($"Un coffre existe déjà : {path}");

        var vaultId = Guid.NewGuid();
        var dataKey = RandomNumberGenerator.GetBytes(SealedBox.KeySize);
        var header = BuildHeader(vaultId, login, password, hardware.GetFingerprint(), iterations, dataKey,
            data: SealedBoxDto.From(SealedBox.Seal(dataKey, "[]"u8.ToArray(), DataAad(vaultId))));

        var session = new VaultSession(path, hardware, vaultId, iterations, dataKey, header, []);
        session.Save();
        var kit = session.CreateRecoveryKit(recoveryIterations);
        return (session, kit);
    }

    // ---------------------------------------------------------------- ouverture

    /// <exception cref="VaultException">Identifiants incorrects, matériel changé ou fichier invalide.</exception>
    public static VaultSession Unlock(string path, string login, string password, IHardwareFingerprintProvider hardware)
    {
        var header = ReadVaultFile(path);
        var salt = Convert.FromBase64String(header.Kdf.Salt);
        var fingerprint = hardware.GetFingerprint();

        var masterKey = KeyDerivation.DeriveMasterKey(login, password, salt, header.Kdf.Iterations, fingerprint);
        byte[] dataKey;
        try
        {
            dataKey = header.WrappedDataKey.ToBox().Open(masterKey, WrapAad(header.VaultId));
        }
        catch (CryptographicException ex)
        {
            var sameHardware = CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(header.HardwareCheck),
                Convert.FromBase64String(KeyDerivation.HardwareCheck(fingerprint, salt)));
            throw sameHardware
                ? new VaultException(VaultError.WrongCredentials, "Identifiant ou mot de passe incorrect.", ex)
                : new VaultException(VaultError.HardwareChanged,
                    "Le processeur de cet ordinateur ne correspond pas à celui qui a créé le coffre. " +
                    "Utilisez votre fichier et votre code de récupération.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }

        var entries = DecryptEntries(header, dataKey);
        return new VaultSession(path, hardware, header.VaultId, header.Kdf.Iterations, dataKey, header, entries);
    }

    /// <summary>
    /// Ouvre le coffre avec le fichier + le code de récupération (après un changement de matériel ou
    /// un oubli du mot de passe), puis le re-chiffre avec les nouveaux identifiants et le processeur actuel.
    /// </summary>
    public static VaultSession Recover(
        string vaultPath, string recoveryFileContent, string recoveryCode,
        string newLogin, string newPassword, IHardwareFingerprintProvider hardware)
    {
        ValidateCredentials(newLogin, newPassword);
        var header = ReadVaultFile(vaultPath);
        var recovery = ParseRecoveryFile(recoveryFileContent);

        if (recovery.VaultId != header.VaultId)
            throw new VaultException(VaultError.RecoveryFileMismatch,
                "Ce fichier de récupération appartient à un autre coffre.");

        var recoveryKey = KeyDerivation.DeriveRecoveryKey(recoveryCode,
            Convert.FromBase64String(recovery.Kdf.Salt), recovery.Kdf.Iterations);
        byte[] dataKey;
        try
        {
            dataKey = recovery.WrappedDataKey.ToBox().Open(recoveryKey, RecoveryAad(recovery.VaultId));
        }
        catch (CryptographicException ex)
        {
            throw new VaultException(VaultError.WrongRecoveryCode, "Code de récupération incorrect.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(recoveryKey);
        }

        var entries = DecryptEntries(header, dataKey);
        var session = new VaultSession(vaultPath, hardware, header.VaultId, header.Kdf.Iterations, dataKey, header, entries);
        session.ChangeCredentials(newLogin, newPassword);
        return session;
    }

    // ---------------------------------------------------------------- enregistrements

    public VaultEntry Add(string name, string login, string password)
    {
        ThrowIfDisposed();
        var entry = new VaultEntry { Name = name.Trim(), Login = login, Password = password };
        _entries.Add(entry);
        Save();
        return entry;
    }

    public void Update(VaultEntry updated)
    {
        ThrowIfDisposed();
        var index = _entries.FindIndex(e => e.Id == updated.Id);
        if (index < 0) throw new KeyNotFoundException("Enregistrement introuvable.");
        var copy = updated.Clone();
        copy.Name = copy.Name.Trim();
        copy.UpdatedUtc = DateTimeOffset.UtcNow;
        _entries[index] = copy;
        Save();
    }

    public bool Remove(Guid id)
    {
        ThrowIfDisposed();
        var removed = _entries.RemoveAll(e => e.Id == id) > 0;
        if (removed) Save();
        return removed;
    }

    // ---------------------------------------------------------------- sécurité

    /// <summary>Change l'identifiant et/ou le mot de passe maître (le contenu n'est pas re-chiffré).</summary>
    public void ChangeCredentials(string newLogin, string newPassword)
    {
        ThrowIfDisposed();
        ValidateCredentials(newLogin, newPassword);
        _header = BuildHeader(_vaultId, newLogin, newPassword, _hardware.GetFingerprint(), _iterations, _dataKey, _header.Data);
        Save();
    }

    /// <summary>
    /// Génère un nouveau fichier + code de récupération. Les anciens restent valides
    /// tant que la clé de données n'a pas changé : détruisez-les si vous les remplacez.
    /// </summary>
    public RecoveryKit CreateRecoveryKit(int iterations = KeyDerivation.DefaultRecoveryIterations)
    {
        ThrowIfDisposed();
        var code = RecoveryCode.Generate();
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = KeyDerivation.DeriveRecoveryKey(code, salt, iterations);
        try
        {
            var model = new RecoveryFileModel
            {
                VaultId = _vaultId,
                CreatedUtc = DateTimeOffset.UtcNow,
                Kdf = new KdfDto(RecoveryKdfAlgorithm, Convert.ToBase64String(salt), iterations),
                WrappedDataKey = SealedBoxDto.From(SealedBox.Seal(key, _dataKey, RecoveryAad(_vaultId))),
            };
            return new RecoveryKit(code, JsonSerializer.Serialize(model, Json.Options));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    // ---------------------------------------------------------------- persistance

    public void Save()
    {
        ThrowIfDisposed();
        var json = JsonSerializer.SerializeToUtf8Bytes(_entries, Json.Options);
        try
        {
            _header = _header with { Data = SealedBoxDto.From(SealedBox.Seal(_dataKey, json, DataAad(_vaultId))) };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // Écriture atomique : fichier temporaire puis remplacement (avec copie .bak).
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(_header, Json.Options), Encoding.UTF8);
        if (File.Exists(_path))
            File.Replace(temp, _path, _path + ".bak", ignoreMetadataErrors: true);
        else
            File.Move(temp, _path);
    }

    public void Dispose()
    {
        if (_disposed) return;
        CryptographicOperations.ZeroMemory(_dataKey);
        _dataKey = [];
        _entries = [];
        _disposed = true;
    }

    // ---------------------------------------------------------------- outils internes

    private static VaultFileModel BuildHeader(Guid vaultId, string login, string password, string fingerprint,
        int iterations, byte[] dataKey, SealedBoxDto data)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var masterKey = KeyDerivation.DeriveMasterKey(login, password, salt, iterations, fingerprint);
        try
        {
            return new VaultFileModel
            {
                VaultId = vaultId,
                Kdf = new KdfDto(KdfAlgorithm, Convert.ToBase64String(salt), iterations),
                HardwareCheck = KeyDerivation.HardwareCheck(fingerprint, salt),
                WrappedDataKey = SealedBoxDto.From(SealedBox.Seal(masterKey, dataKey, WrapAad(vaultId))),
                Data = data,
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    private static List<VaultEntry> DecryptEntries(VaultFileModel header, byte[] dataKey)
    {
        try
        {
            var json = header.Data.ToBox().Open(dataKey, DataAad(header.VaultId));
            try
            {
                return JsonSerializer.Deserialize<List<VaultEntry>>(json, Json.Options) ?? [];
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            CryptographicOperations.ZeroMemory(dataKey);
            throw new VaultException(VaultError.InvalidFile, "Le contenu du coffre est endommagé.", ex);
        }
    }

    private static VaultFileModel ReadVaultFile(string path)
    {
        try
        {
            var model = JsonSerializer.Deserialize<VaultFileModel>(File.ReadAllText(path), Json.Options);
            if (model is null || model.Format != VaultFileModel.FormatName || model.Version != 1)
                throw new VaultException(VaultError.InvalidFile, "Ce fichier n'est pas un coffre Personnal-Pass.");
            return model;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or NotSupportedException)
        {
            throw new VaultException(VaultError.InvalidFile, "Le fichier du coffre est illisible.", ex);
        }
    }

    private static RecoveryFileModel ParseRecoveryFile(string content)
    {
        try
        {
            var model = JsonSerializer.Deserialize<RecoveryFileModel>(content, Json.Options);
            if (model is null || model.Format != RecoveryFileModel.FormatName || model.Version != 1)
                throw new VaultException(VaultError.InvalidFile, "Ce fichier n'est pas un fichier de récupération Personnal-Pass.");
            return model;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or NotSupportedException)
        {
            throw new VaultException(VaultError.InvalidFile, "Le fichier de récupération est illisible.", ex);
        }
    }

    private static void ValidateCredentials(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("L'identifiant est obligatoire.", nameof(login));
        if (password is null || password.Length < 8)
            throw new ArgumentException("Le mot de passe maître doit contenir au moins 8 caractères.", nameof(password));
    }

    private static byte[] WrapAad(Guid id) => Encoding.UTF8.GetBytes($"PersonnalPass/wrap/{id}");
    private static byte[] DataAad(Guid id) => Encoding.UTF8.GetBytes($"PersonnalPass/data/{id}");
    private static byte[] RecoveryAad(Guid id) => Encoding.UTF8.GetBytes($"PersonnalPass/recovery/{id}");

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
