namespace ReturnLoad.Infrastructure.Security;

/// <summary>
/// Field-encryption settings, bound from the "Encryption" section. <see cref="Key"/> is a
/// base64-encoded 256-bit key supplied via environment/secret store (never committed,
/// 01_PROJECT_RULES.md §1). Missing outside Development is a fail-on-use error.
/// </summary>
public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";

    /// <summary>Base64-encoded 32-byte AES key.</summary>
    public string Key { get; init; } = string.Empty;
}
