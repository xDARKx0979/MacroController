using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MacroController.App.Update;

/// <summary>AES helper used to encrypt/decrypt the update manifest.</summary>
internal static class Crypto
{
    // Key/IV for the update manifest only - not a secret boundary, just keeps
    // manifest.dat from being readable as plain JSON in the repo.
    private const string KeyHex = "62cd71acaedb737188a7b2e221972a77e43271238bfdfe4d7db9f01283bd5012";
    private const string IvHex = "28681a9f78d925cbdfff513ada5127a9";

    public static string Decrypt(byte[] bytes)
    {
        using var aes = Aes.Create();
        using var decryptor = aes.CreateDecryptor(HexToBytes(KeyHex), HexToBytes(IvHex));
        using var msDecrypt = new MemoryStream(bytes);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        return srDecrypt.ReadToEnd();
    }

    public static byte[] Encrypt(string content)
    {
        using var aes = Aes.Create();
        using var encryptor = aes.CreateEncryptor(HexToBytes(KeyHex), HexToBytes(IvHex));
        using var msEncrypt = new MemoryStream();
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
            swEncrypt.Write(content);
        return msEncrypt.ToArray();
    }

    public static byte[] HexToBytes(string hex)
    {
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    public static string BytesToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
