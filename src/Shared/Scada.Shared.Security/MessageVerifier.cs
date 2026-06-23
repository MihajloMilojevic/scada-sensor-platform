using System.Security.Cryptography;
using System.Text;

namespace Scada.Shared.Security;

public static class MessageVerifier
{
    // publicKeySpki = SubjectPublicKeyInfo DER bytes of an EC P-256 public key
    public static bool Verify(string message, string signatureBase64, byte[] publicKeySpki)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(publicKeySpki, out _);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var signature = Convert.FromBase64String(signatureBase64);
            return ecdsa.VerifyData(messageBytes, signature, HashAlgorithmName.SHA256);
        }
        catch { return false; }
    }
}
