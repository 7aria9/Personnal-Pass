using System.Text.Json;
using System.Text.Json.Serialization;
using PersonnalPass.Core.Crypto;

namespace PersonnalPass.Core.Vault;

internal sealed record SealedBoxDto(string Nonce, string CipherText, string Tag)
{
    public static SealedBoxDto From(SealedBox box) => new(
        Convert.ToBase64String(box.Nonce),
        Convert.ToBase64String(box.CipherText),
        Convert.ToBase64String(box.Tag));

    public SealedBox ToBox() => new(
        Convert.FromBase64String(Nonce),
        Convert.FromBase64String(CipherText),
        Convert.FromBase64String(Tag));
}

internal sealed record KdfDto(string Algorithm, string Salt, int Iterations);

/// <summary>Fichier coffre (vault.ppv). Aucune donnée personnelle n'y est en clair.</summary>
internal sealed record VaultFileModel
{
    public const string FormatName = "PersonnalPass-Vault";

    public string Format { get; init; } = FormatName;
    public int Version { get; init; } = 1;
    public Guid VaultId { get; init; }
    public required KdfDto Kdf { get; init; }
    public required string HardwareCheck { get; init; }
    /// <summary>Clé de données chiffrée par la clé maître (identifiant + mot de passe + processeur).</summary>
    public required SealedBoxDto WrappedDataKey { get; init; }
    /// <summary>Liste des enregistrements (JSON) chiffrée par la clé de données.</summary>
    public required SealedBoxDto Data { get; init; }
}

/// <summary>
/// Fichier de récupération (.pprecovery) : contient la clé de données,
/// elle-même chiffrée par le code de récupération. Inutilisable sans ce code.
/// </summary>
internal sealed record RecoveryFileModel
{
    public const string FormatName = "PersonnalPass-Recovery";

    public string Format { get; init; } = FormatName;
    public int Version { get; init; } = 1;
    public Guid VaultId { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
    public required KdfDto Kdf { get; init; }
    public required SealedBoxDto WrappedDataKey { get; init; }
}

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
