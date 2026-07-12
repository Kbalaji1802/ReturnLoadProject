using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ReturnLoad.Application.Abstractions.Security;

namespace ReturnLoad.Infrastructure.Security;

/// <summary>
/// AES-GCM authenticated field encryption (ADR-0015). Output layout, base64-encoded:
/// <c>[12-byte nonce][16-byte tag][ciphertext]</c>. A fresh random nonce per call means the
/// same plaintext encrypts differently each time (so encrypted columns are not
/// deterministically indexable — fine for Aadhaar, which is masked in UI and never a key).
/// The key is read lazily; using the encryptor without a configured key fails loudly.
/// </summary>
public sealed class AesFieldEncryptor : IFieldEncryptor
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[]? _key;

    public AesFieldEncryptor(IOptions<EncryptionOptions> options)
    {
        string configured = options.Value.Key;
        _key = string.IsNullOrWhiteSpace(configured) ? null : Convert.FromBase64String(configured);
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        byte[] key = RequireKey();

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] cipher = new byte[plainBytes.Length];
        byte[] tag = new byte[TagSize];

        using (AesGcm aes = new(key, TagSize))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }

        byte[] output = [.. nonce, .. tag, .. cipher];
        return Convert.ToBase64String(output);
    }

    public string Decrypt(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        byte[] key = RequireKey();

        byte[] input = Convert.FromBase64String(ciphertext);
        ReadOnlySpan<byte> span = input;
        ReadOnlySpan<byte> nonce = span[..NonceSize];
        ReadOnlySpan<byte> tag = span.Slice(NonceSize, TagSize);
        ReadOnlySpan<byte> cipher = span[(NonceSize + TagSize)..];

        byte[] plain = new byte[cipher.Length];
        using (AesGcm aes = new(key, TagSize))
        {
            aes.Decrypt(nonce, cipher, tag, plain);
        }

        return Encoding.UTF8.GetString(plain);
    }

    private byte[] RequireKey() =>
        _key ?? throw new InvalidOperationException(
            "Encryption:Key is not configured; cannot encrypt/decrypt sensitive fields. Supply a base64 256-bit key via a secret/environment variable.");
}
