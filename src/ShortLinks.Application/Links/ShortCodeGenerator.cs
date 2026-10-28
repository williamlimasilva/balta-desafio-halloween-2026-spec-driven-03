using System.Security.Cryptography;

namespace ShortLinks.Application.Links;

public sealed class ShortCodeGenerator
{
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
    public const int GeneratedCodeLength = 7;

    public string Generate()
    {
        return string.Create(GeneratedCodeLength, 0, static (characters, _) =>
        {
            for (var index = 0; index < characters.Length; index++)
            {
                characters[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            }
        });
    }

    public static bool IsValidCustomCode(string? code)
    {
        if (code is null || code.Length is < 4 or > 20)
        {
            return false;
        }

        return code.All(Alphabet.Contains);
    }
}