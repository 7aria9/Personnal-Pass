using System.Security.Cryptography;
using System.Text;

namespace PersonnalPass.Core.Crypto;

/// <summary>Dérivation des clés de chiffrement à partir des secrets de l'utilisateur.</summary>
public static class KeyDerivation
{
    /// <summary>Recommandation OWASP 2023+ pour PBKDF2-HMAC-SHA256.</summary>
    public const int DefaultPasswordIterations = 600_000;

    /// <summary>Le code de récupération est aléatoire (≈125 bits) : moins d'itérations suffisent.</summary>
    public const int DefaultRecoveryIterations = 200_000;

    private static readonly byte[] MasterKeyInfo = Encoding.UTF8.GetBytes("PersonnalPass/master-key/v1");

    /// <summary>
    /// Clé maître = HKDF( PBKDF2(identifiant + mot de passe), empreinte matérielle ).
    /// Il faut donc à la fois l'identifiant, le mot de passe ET le même processeur.
    /// </summary>
    public static byte[] DeriveMasterKey(string login, string password, byte[] salt, int iterations, string hardwareFingerprint)
    {
        var secret = Encoding.UTF8.GetBytes(NormalizeLogin(login) + "\0" + password);
        try
        {
            var passwordKey = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, SealedBox.KeySize);
            try
            {
                var hardware = SHA256.HashData(Encoding.UTF8.GetBytes(hardwareFingerprint));
                return HKDF.DeriveKey(HashAlgorithmName.SHA256, passwordKey, SealedBox.KeySize, hardware, MasterKeyInfo);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(passwordKey);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public static byte[] DeriveRecoveryKey(string recoveryCode, byte[] salt, int iterations)
    {
        var secret = Encoding.UTF8.GetBytes(RecoveryCode.Normalize(recoveryCode));
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, HashAlgorithmName.SHA256, SealedBox.KeySize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>
    /// Empreinte salée du matériel, stockée pour distinguer « mauvais mot de passe »
    /// de « changement de matériel » sans révéler l'identifiant du processeur.
    /// </summary>
    public static string HardwareCheck(string hardwareFingerprint, byte[] salt)
    {
        var mac = HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes("hw-check\0" + hardwareFingerprint));
        return Convert.ToBase64String(mac);
    }

    public static string NormalizeLogin(string login) => login.Trim().ToLowerInvariant();
}
