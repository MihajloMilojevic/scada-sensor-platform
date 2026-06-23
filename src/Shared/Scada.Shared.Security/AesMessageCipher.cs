using System.Security.Cryptography;
using System.Text;

namespace Scada.Shared.Security;

public static class AesMessageCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    // Returns base64(nonce[12] + tag[16] + ciphertext)
    public static string Encrypt(string plaintext, byte[] key)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var combined = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(combined, 0);
        tag.CopyTo(combined, NonceSize);
        ciphertext.CopyTo(combined, NonceSize + TagSize);
        return Convert.ToBase64String(combined);
    }

    public static string Decrypt(string base64Ciphertext, byte[] key)
    {
        var combined = Convert.FromBase64String(base64Ciphertext);
        var nonce = combined[..NonceSize];
        var tag = combined[NonceSize..(NonceSize + TagSize)];
        var ciphertext = combined[(NonceSize + TagSize)..];

        var plaintext = new byte[ciphertext.Length];
        using var aesGcm = new AesGcm(key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }
}
