namespace PersonnalPass.App.Views;

internal static class CredentialRules
{
    public const int MinimumLength = 8;

    /// <returns>Un message d'erreur, ou null si tout est correct.</returns>
    public static string? Validate(string login, string password, string confirmation)
    {
        if (string.IsNullOrWhiteSpace(login)) return "L'identifiant est obligatoire.";
        if (password.Length < MinimumLength) return $"Le mot de passe maître doit contenir au moins {MinimumLength} caractères.";
        if (password != confirmation) return "Les deux mots de passe ne correspondent pas.";
        return null;
    }
}
