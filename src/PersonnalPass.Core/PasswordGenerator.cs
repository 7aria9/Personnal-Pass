using System.Security.Cryptography;

namespace PersonnalPass.Core;

public sealed record PasswordOptions(
    int Length = 16,
    bool Uppercase = true,
    bool Lowercase = true,
    bool Digits = true,
    bool Special = true)
{
    public bool HasAnyCharacterSet => Uppercase || Lowercase || Digits || Special;
}

/// <summary>Générateur de mots de passe aléatoires (générateur cryptographique du système).</summary>
public static class PasswordGenerator
{
    public const int MinLength = 8;
    public const int MaxLength = 32;

    public const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    public const string DigitChars = "0123456789";
    public const string SpecialChars = "!@#$%^&*()-_=+[]{};:,.?/~";

    /// <summary>
    /// Génère un mot de passe contenant au moins un caractère de chaque famille cochée.
    /// </summary>
    public static string Generate(PasswordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Length is < MinLength or > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(options), $"La longueur doit être comprise entre {MinLength} et {MaxLength}.");
        if (!options.HasAnyCharacterSet)
            throw new ArgumentException("Cochez au moins un type de caractères.", nameof(options));

        var sets = new List<string>(4);
        if (options.Uppercase) sets.Add(UppercaseChars);
        if (options.Lowercase) sets.Add(LowercaseChars);
        if (options.Digits) sets.Add(DigitChars);
        if (options.Special) sets.Add(SpecialChars);

        var all = string.Concat(sets);
        var result = new char[options.Length];

        // Un caractère garanti par famille choisie…
        for (var i = 0; i < sets.Count; i++)
            result[i] = Pick(sets[i]);
        // …puis le reste au hasard dans l'ensemble des familles.
        for (var i = sets.Count; i < result.Length; i++)
            result[i] = Pick(all);

        // Mélange de Fisher-Yates pour que les caractères garantis ne soient pas en tête.
        for (var i = result.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }
        return new string(result);
    }

    private static char Pick(string chars) => chars[RandomNumberGenerator.GetInt32(chars.Length)];
}
