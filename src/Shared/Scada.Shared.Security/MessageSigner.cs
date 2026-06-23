using System.Security.Cryptography;
using System.Text;

namespace Scada.Shared.Security;

public static class MessageSigner
{
    // privateKeyPkcs8 = PKCS#8 DER bytes of an EC P-256 private key
    public static string Sign(string message, byte[] privateKeyPkcs8)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(privateKeyPkcs8, out _);
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var signature = ecdsa.SignData(messageBytes, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(signature);
    }
}
