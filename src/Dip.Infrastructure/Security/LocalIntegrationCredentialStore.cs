// Purpose: Implements L oc al In te gr at io nC re de nt ia lS to re infrastructure integration for s ec ur it y.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Security;

namespace Dip.Infrastructure.Security;

/// <summary>
/// Difficulty-1 local secret store. Secret bodies are kept out of PostgreSQL and
/// out of the repository. Files are AES-GCM encrypted under the current user's
/// local application-data directory. Enterprise vault providers replace this
/// implementation in the later Credential Vault milestone.
/// </summary>
public sealed class LocalIntegrationCredentialStore : IIntegrationCredentialStore
{
    private const string Scheme = "local-secret://";
    private const byte FormatVersion = 1;
    private readonly string _root;
    private readonly string _keyPath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalIntegrationCredentialStore()
    {
        var local = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".dip");

        _root = Path.Combine(local, "DIP", "integration-secrets");
        _keyPath = Path.Combine(_root, "master.key");
    }

    public async Task<string> StoreAsync(
        string provider,
        string secretJson,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));
        if (string.IsNullOrWhiteSpace(secretJson))
            throw new ArgumentException("Secret JSON is required.", nameof(secretJson));

        // Validate before writing anything.
        using (JsonDocument.Parse(secretJson)) { }

        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_root);
            var key = await GetOrCreateKeyAsync(ct);
            var id = Guid.NewGuid();
            var payload = JsonSerializer.Serialize(new SecretEnvelope(provider, secretJson));
            var encrypted = Encrypt(key, Encoding.UTF8.GetBytes(payload));
            var path = SecretPath(id);
            var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");

            await File.WriteAllBytesAsync(temp, encrypted, ct);
            File.Move(temp, path, overwrite: false);
            BestEffortRestrict(path);
            return Scheme + id.ToString("N");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> GetAsync(
        string credentialReference,
        CancellationToken ct)
    {
        var id = ParseReference(credentialReference);
        var path = SecretPath(id);
        if (!File.Exists(path))
            return null;

        await _gate.WaitAsync(ct);
        try
        {
            var key = await GetOrCreateKeyAsync(ct);
            var encrypted = await File.ReadAllBytesAsync(path, ct);
            var plain = Decrypt(key, encrypted);
            var envelope = JsonSerializer.Deserialize<SecretEnvelope>(
                Encoding.UTF8.GetString(plain));
            return envelope?.SecretJson;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(
        string credentialReference,
        CancellationToken ct)
    {
        var id = ParseReference(credentialReference);
        await _gate.WaitAsync(ct);
        try
        {
            var path = SecretPath(id);
            if (File.Exists(path))
                File.Delete(path);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<byte[]> GetOrCreateKeyAsync(CancellationToken ct)
    {
        if (File.Exists(_keyPath))
        {
            var existing = await File.ReadAllBytesAsync(_keyPath, ct);
            if (existing.Length != 32)
                throw new InvalidOperationException(
                    "DIP local integration-secret master key has an invalid length.");
            return existing;
        }

        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            await using var stream = new FileStream(
                _keyPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous);
            await stream.WriteAsync(key, ct);
            await stream.FlushAsync(ct);
            BestEffortRestrict(_keyPath);
            return key;
        }
        catch (IOException) when (File.Exists(_keyPath))
        {
            var existing = await File.ReadAllBytesAsync(_keyPath, ct);
            if (existing.Length != 32)
                throw;
            return existing;
        }
    }

    private static byte[] Encrypt(byte[] key, byte[] plain)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[plain.Length];
        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        var output = new byte[1 + nonce.Length + tag.Length + cipher.Length];
        output[0] = FormatVersion;
        Buffer.BlockCopy(nonce, 0, output, 1, nonce.Length);
        Buffer.BlockCopy(tag, 0, output, 13, tag.Length);
        Buffer.BlockCopy(cipher, 0, output, 29, cipher.Length);
        return output;
    }

    private static byte[] Decrypt(byte[] key, byte[] encrypted)
    {
        if (encrypted.Length < 30 || encrypted[0] != FormatVersion)
            throw new InvalidDataException("Unsupported integration-secret format.");

        var nonce = encrypted.AsSpan(1, 12).ToArray();
        var tag = encrypted.AsSpan(13, 16).ToArray();
        var cipher = encrypted.AsSpan(29).ToArray();
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, tagSizeInBytes: 16);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private Guid ParseReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !value.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(value[Scheme.Length..], "N", out var id))
            throw new ArgumentException(
                "Credential reference is invalid.",
                nameof(value));
        return id;
    }

    private string SecretPath(Guid id)
        => Path.Combine(_root, id.ToString("N") + ".secret");

    private static void BestEffortRestrict(string path)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // A filesystem may not support POSIX modes. Encryption still applies.
        }
    }

    private sealed record SecretEnvelope(string Provider, string SecretJson);
}
