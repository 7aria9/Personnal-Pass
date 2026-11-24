namespace PersonnalPass.Core.Vault;

/// <summary>Fournit une signature stable du matériel (ex. identifiant du processeur).</summary>
public interface IHardwareFingerprintProvider
{
    string GetFingerprint();
}
