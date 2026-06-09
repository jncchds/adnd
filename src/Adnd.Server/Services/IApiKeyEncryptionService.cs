using System.Security.Cryptography;
using System.Text;

namespace Adnd.Server.Services;

/// <summary>
/// AES-256-GCM encryption service for protecting API keys and other secrets at rest.
/// Keys are derived from a master key configured in appsettings.json.
/// </summary>
public interface IApiKeyEncryptionService
{
    /// <summary>
    /// Encrypt a plaintext string. Returns Base64-encoded ciphertext with IV prepended.
    /// Format: IV(12 bytes) + ciphertext + auth tag (16 bytes).
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypt a Base64-encoded ciphertext (IV + ciphertext + tag).
    /// </summary>
    string Decrypt(string ciphertext);
}

public class ApiKeyEncryptionService : IApiKeyEncryptionService
{
    private readonly byte[] _key;

    public ApiKeyEncryptionService(IConfiguration configuration)
    {
        var masterKey = configuration["Encryption:MasterKey"]
            ?? throw new InvalidOperationException("Encryption master key not configured. Set 'Encryption:MasterKey' in appsettings.");

        // Derive a 32-byte key from the master key using SHA-256
        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(masterKey));
    }

    public string Encrypt(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        using var aes = new AesGcm(_key, 16);
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        aes.Encrypt(nonce,
            Encoding.UTF8.GetBytes(plaintext),
            ciphertext,
            tag);

        // Prepend IV to ciphertext for storage
        var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length + ciphertext.Length, tag.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        var data = Convert.FromBase64String(ciphertext);

        if (data.Length < 12 + 16) // minimum: nonce(12) + tag(16)
            throw new CryptographicException("Ciphertext too short.");

        using var aes = new AesGcm(_key, 16);

        var nonce = new byte[12];
        var tag = new byte[16];
        var encrypted = new byte[data.Length - 12 - 16];

        Buffer.BlockCopy(data, 0, nonce, 0, 12);
        Buffer.BlockCopy(data, 12 + encrypted.Length, tag, 0, 16);
        Buffer.BlockCopy(data, 12, encrypted, 0, encrypted.Length);

        var plaintext = new byte[encrypted.Length];
        aes.Decrypt(nonce, encrypted, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
