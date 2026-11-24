using System.Security.Cryptography;

namespace PersonnalPass.Core.Crypto;

/// <summary>
/// Bloc chiffré AES-256-GCM : nonce + texte chiffré + tag d'authentification.
/// Toute modification du contenu (ou une mauvaise clé) est détectée au déchiffrement.
/// </summary>
public sealed record SealedBox(byte[] Nonce, byte[] CipherText, byte[] Tag)
{
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;

    public static SealedBox Seal(byte[] key, byte[] plainText, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySize) throw new ArgumentException("La clé doit faire 256 bits.", nameof(key));

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainText.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plainText, cipher, tag, associatedData);
        return new SealedBox(nonce, cipher, tag);
    }

    /// <exception cref="CryptographicException">Clé incorrecte ou données altérées.</exception>
    public byte[] Open(byte[] key, byte[]? associatedData = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length != KeySize) throw new ArgumentException("La clé doit faire 256 bits.", nameof(key));
        if (Nonce.Length != NonceSize || Tag.Length != TagSize)
            throw new CryptographicException("Bloc chiffré invalide.");

        var plain = new byte[CipherText.Length];
        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(Nonce, CipherText, Tag, plain, associatedData);
        return plain;
    }
}
