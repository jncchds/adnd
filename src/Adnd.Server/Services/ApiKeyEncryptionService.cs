using System.Security.Cryptography;
using System.Text;

namespace Adnd.Server.Services;

public interface IApiKeyEncryptionService
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}

public class ApiKeyEncryptionService : IApiKeyEncryptionService
{
    private readonly byte[] _key;

    public ApiKeyEncryptionService(IConfiguration configuration)
    {
        var masterKey = configuration["Encryption:MasterKey"]
            ?? throw new InvalidOperationException("Encryption:MasterKey is not configured.");
        _key = Convert.FromBase64String(masterKey);
        if (_key.Length != 32)
            throw new InvalidOperationException("Encryption:MasterKey must be a 32-byte base64-encoded key.");
    }

    public string Encrypt(string plaintext)
    {
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, nonce.Length);
        ciphertext.CopyTo(result, nonce.Length + tag.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string ciphertext)
    {
        byte[] data;
        try
        {
            data = Convert.FromBase64String(ciphertext);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Stored API key is not valid base64.", ex);
        }

        // Layout is nonce(12) ‖ tag(16) ‖ ciphertext. Slicing a shorter buffer threw
        // ArgumentOutOfRangeException, surfacing as an unhandled 500 rather than a
        // clear error about corrupt or wrongly-keyed data.
        if (data.Length < 28)
            throw new InvalidOperationException("Stored API key is malformed or truncated.");

        var nonce = data[..12];
        var tag = data[12..28];
        var encryptedData = data[28..];

        var plaintext = new byte[encryptedData.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, encryptedData, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
