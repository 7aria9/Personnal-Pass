using System.Security.Cryptography;
using System.Text;

namespace PersonnalPass.Core.Crypto;

/// <summary>
/// Code de récupération lisible : 5 groupes de 5 caractères (alphabet sans 0/O/1/I/L),
/// soit environ 122 bits d'entropie. Exemple : K7QXM-2HD9R-WPZ4T-B8NCE-3YVJA
/// </summary>
public static class RecoveryCode
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int Groups = 5;
    private const int GroupLength = 5;

    public static string Generate()
    {
        var sb = new StringBuilder(Groups * (GroupLength + 1));
        for (var g = 0; g < Groups; g++)
        {
            if (g > 0) sb.Append('-');
            for (var i = 0; i < GroupLength; i++)
                sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }
        return sb.ToString();
    }

    /// <summary>Ignore espaces, tirets et casse pour tolérer les fautes de saisie.</summary>
    public static string Normalize(string code)
    {
        var sb = new StringBuilder(code.Length);
        foreach (var c in code)
        {
            if (char.IsWhiteSpace(c) || c == '-') continue;
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    public static bool LooksValid(string code)
    {
        var n = Normalize(code);
        return n.Length == Groups * GroupLength && n.All(Alphabet.Contains);
    }
}
