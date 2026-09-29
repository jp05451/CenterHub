using System.Security.Cryptography;

namespace CenterHub.Data;

/// <summary>Generates the one-time password for the seeded admin account.</summary>
public static class AdminSeedPassword
{
    // Excludes visually ambiguous characters (0/O, 1/l/I) since the generated password is
    // read from the startup log and retyped by hand.
    private const string AllowedCharacters = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*";

    public static string Generate(int length = 12)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            var index = RandomNumberGenerator.GetInt32(AllowedCharacters.Length);
            chars[i] = AllowedCharacters[index];
        }
        return new string(chars);
    }
}
