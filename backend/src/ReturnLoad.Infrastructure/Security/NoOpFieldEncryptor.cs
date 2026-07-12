using ReturnLoad.Application.Abstractions.Security;

namespace ReturnLoad.Infrastructure.Security;

/// <summary>
/// Identity "encryptor" used only at design time (EF migrations), where the model shape —
/// not real ciphertext — is all that matters. Never registered for runtime use.
/// </summary>
public sealed class NoOpFieldEncryptor : IFieldEncryptor
{
    public string Encrypt(string plaintext) => plaintext;

    public string Decrypt(string ciphertext) => ciphertext;
}
