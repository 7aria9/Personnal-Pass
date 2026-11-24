namespace PersonnalPass.Core.Vault;

public enum VaultError
{
    /// <summary>Identifiant ou mot de passe incorrect.</summary>
    WrongCredentials,
    /// <summary>Le processeur n'est pas celui qui a créé le coffre : utiliser la récupération.</summary>
    HardwareChanged,
    /// <summary>Code de récupération incorrect ou fichier de récupération altéré.</summary>
    WrongRecoveryCode,
    /// <summary>Le fichier de récupération ne correspond pas à ce coffre.</summary>
    RecoveryFileMismatch,
    /// <summary>Fichier illisible, d'un autre format ou endommagé.</summary>
    InvalidFile,
}

public sealed class VaultException(VaultError error, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public VaultError Error { get; } = error;
}
