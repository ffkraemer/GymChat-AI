using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;

namespace GymChatAI.Infrastructure.WhatsApp;

public record DecryptedFlowRequest(string Json, byte[] AesKey, byte[] RequestIv);

/// <summary>
/// Encryption/decryption for the WhatsApp Flows Data Exchange endpoint. This is the single
/// highest-risk piece of the whole Flows integration: it cannot be meaningfully unit-tested
/// without a real encrypted payload from Meta, and a subtle mistake (wrong tag length, wrong
/// key format, missing IV flip) fails silently - every single Flow interaction breaks the
/// same way. Follows the pattern documented across Meta's own examples and third-party SDKs
/// (Node.js, Python, Elixir): RSA-OAEP(SHA-256) to unwrap the AES key, AES-128-GCM (tag
/// appended to ciphertext) for the payload, and a full bit-flip of the request's IV to derive
/// the response's IV, reusing the same AES key.
/// </summary>
public class WhatsAppFlowEncryptionService
{
    /// <summary>Decrypts an incoming Data Exchange request. Returns the plaintext JSON plus the AES key/IV needed to encrypt the reply.</summary>
    public DecryptedFlowRequest DecryptRequest(
        string encryptedFlowDataBase64, string encryptedAesKeyBase64, string initialVectorBase64, string privateKeyPem, string? privateKeyPassword = null)
    {
        var privateKey = LoadPrivateKey(privateKeyPem, privateKeyPassword);

        // 1. Unwrap the AES key using RSA-OAEP (SHA-256, per Meta's spec).
        var encryptedAesKey = Convert.FromBase64String(encryptedAesKeyBase64);
        var oaep = new OaepEncoding(new RsaEngine(), new Org.BouncyCastle.Crypto.Digests.Sha256Digest());
        oaep.Init(false, privateKey);
        var aesKey = oaep.ProcessBlock(encryptedAesKey, 0, encryptedAesKey.Length);

        // 2. Decrypt the payload with AES-128-GCM. Meta appends the 16-byte auth tag to the
        // end of the ciphertext, which is exactly the layout BouncyCastle's GcmBlockCipher
        // expects as a single combined input.
        var iv = Convert.FromBase64String(initialVectorBase64);
        var cipherTextWithTag = Convert.FromBase64String(encryptedFlowDataBase64);

        var gcm = new GcmBlockCipher(new AesEngine());
        gcm.Init(false, new AeadParameters(new KeyParameter(aesKey), 128, iv));

        var plaintext = new byte[gcm.GetOutputSize(cipherTextWithTag.Length)];
        var length = gcm.ProcessBytes(cipherTextWithTag, 0, cipherTextWithTag.Length, plaintext, 0);
        length += gcm.DoFinal(plaintext, length);

        var json = System.Text.Encoding.UTF8.GetString(plaintext, 0, length);
        return new DecryptedFlowRequest(json, aesKey, iv);
    }

    /// <summary>
    /// Encrypts a Data Exchange response. Meta requires every bit of the request's IV to be
    /// flipped (NOT the same IV reused as-is) before encrypting the response with the same AES key.
    /// </summary>
    public string EncryptResponse(string plaintextJson, byte[] aesKey, byte[] requestIv)
    {
        var responseIv = new byte[requestIv.Length];
        for (var i = 0; i < requestIv.Length; i++)
            responseIv[i] = (byte)~requestIv[i];

        var plaintext = System.Text.Encoding.UTF8.GetBytes(plaintextJson);

        var gcm = new GcmBlockCipher(new AesEngine());
        gcm.Init(true, new AeadParameters(new KeyParameter(aesKey), 128, responseIv));

        var output = new byte[gcm.GetOutputSize(plaintext.Length)];
        var length = gcm.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
        length += gcm.DoFinal(output, length);

        return Convert.ToBase64String(output, 0, length);
    }

    private static AsymmetricKeyParameter LoadPrivateKey(string privateKeyPem, string? password)
    {
        using var reader = new StringReader(privateKeyPem);
        object keyObject = password is null
            ? new PemReader(reader).ReadObject()
            : new PemReader(reader, new StaticPasswordFinder(password)).ReadObject();

        return keyObject switch
        {
            AsymmetricCipherKeyPair pair => pair.Private,
            AsymmetricKeyParameter key => key,
            _ => throw new InvalidOperationException("Could not read an RSA private key from the provided PEM.")
        };
    }

    private class StaticPasswordFinder(string password) : IPasswordFinder
    {
        public char[] GetPassword() => password.ToCharArray();
    }
}
