namespace PersonnalPass.App.Services;

public static class AppPaths
{
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersonnalPass");

    public static string VaultFile { get; } = Path.Combine(DataFolder, "vault.ppv");

    public const string RecoveryFilter = "Fichier de récupération Personnal-Pass (*.pprecovery)|*.pprecovery|Tous les fichiers (*.*)|*.*";
}
