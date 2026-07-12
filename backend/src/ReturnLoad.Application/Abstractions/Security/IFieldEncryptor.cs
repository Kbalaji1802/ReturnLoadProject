namespace ReturnLoad.Application.Abstractions.Security;

/// <summary>
/// Encrypts/decrypts sensitive field values at rest (e.g. Aadhaar — Trust &amp; Safety §1,
/// 01_PROJECT_RULES.md §5,6). Used by EF value converters so the plaintext never touches
/// the database. The key is supplied via configuration/secret store, never committed.
/// </summary>
public interface IFieldEncryptor
{
    /// <summary>Encrypts plaintext to an opaque, storable string.</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypts a value previously produced by <see cref="Encrypt"/>.</summary>
    string Decrypt(string ciphertext);
}
