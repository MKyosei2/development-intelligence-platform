// Purpose: Implements M in io Ar ti fa ct St or e infrastructure integration for a rt if ac ts.
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Dip.Application.Abstractions.Artifacts;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Artifacts;

/// <summary>S3 Signature V4 implementation kept dependency-free for MinIO/S3-compatible object storage.</summary>
public sealed class MinioArtifactStore : IArtifactStore, IDisposable
{
    private readonly HttpClient _http = new();
    private readonly ArtifactStoreOptions.MinioOptions _o;
    private readonly SemaphoreSlim _bucketGate = new(1, 1);
    private volatile bool _bucketReady;
    public MinioArtifactStore(IOptions<ArtifactStoreOptions> options) { _o = options.Value.Minio; if (!Uri.TryCreate(_o.Endpoint, UriKind.Absolute, out _)) throw new InvalidOperationException("Artifacts:Minio:Endpoint must be an absolute URI."); }
    public string ProviderName => "minio";
    public async Task<StoredArtifactContent> PutAsync(Stream content, string contentType, CancellationToken ct)
    {
        await EnsureBucketAsync(ct); var temp = Path.GetTempFileName(); long size = 0; string hash;
        try
        {
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true)) using (var sha = SHA256.Create())
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer.AsMemory(), ct)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    size += read;
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                hash = Convert.ToHexString(sha.Hash!).ToLowerInvariant();
            }
            var key = $"sha256/{hash[..2]}/{hash}";
            if (!await ExistsAsync(key, ct))
            {
                await using var file = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                await SendAsync(HttpMethod.Put, key, file, hash, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType, ct);
            }
            return new(key, hash, size, string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim());
        }
        finally { try { File.Delete(temp); } catch (IOException) {
            // Intentionally ignored to preserve the existing best-effort or fallback behavior.
        } }
    }
    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var response = await SendRawAsync(HttpMethod.Get, storageKey, null, EmptyHash, "application/octet-stream", ct);
        if (!response.IsSuccessStatusCode)
        {
            response.Dispose();
            throw new FileNotFoundException($"Artifact object '{storageKey}' was not found.");
        }
        var stream = await response.Content.ReadAsStreamAsync(ct); return new ResponseOwnedStream(stream, response);
    }
    public async Task DeleteAsync(string storageKey, CancellationToken ct) { using var response = await SendRawAsync(HttpMethod.Delete, storageKey, null, EmptyHash, "application/octet-stream", ct); if (response.StatusCode != HttpStatusCode.NotFound) response.EnsureSuccessStatusCode(); }
    public async Task<bool> ExistsAsync(string storageKey, CancellationToken ct) { using var response = await SendRawAsync(HttpMethod.Head, storageKey, null, EmptyHash, "application/octet-stream", ct); return response.IsSuccessStatusCode; }
    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketReady) return;
        await _bucketGate.WaitAsync(ct);
        try
        {
            if (_bucketReady) return;
            using var head = await SendRawAsync(HttpMethod.Head, null, null, EmptyHash, "application/octet-stream", ct);
            if (head.StatusCode == HttpStatusCode.NotFound)
            {
                using var create = await SendRawAsync(HttpMethod.Put, null, null, EmptyHash, "application/octet-stream", ct);
                create.EnsureSuccessStatusCode();
            }
            else head.EnsureSuccessStatusCode();
            _bucketReady = true;
        }
        finally
        {
            _bucketGate.Release();
        }
    }
    private async Task SendAsync(HttpMethod method, string key, Stream body, string payloadHash, string contentType, CancellationToken ct) { using var response = await SendRawAsync(method, key, body, payloadHash, contentType, ct); response.EnsureSuccessStatusCode(); }
    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string? key, Stream? body, string payloadHash, string contentType, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
         var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
         var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var endpoint = new Uri(_o.Endpoint.TrimEnd('/') + "/"); var path = "/" + Escape(_o.Bucket) + (string.IsNullOrEmpty(key) ? string.Empty : "/" + string.Join('/', key.Split('/').Select(Escape)));
        var uri = new Uri(endpoint, path);
        var host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = $"{method.Method}\n{path}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var scope = $"{date}/{_o.Region}/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{HexSha(canonicalRequest)}";
        var signingKey = Hmac(Hmac(Hmac(Hmac(Encoding.UTF8.GetBytes("AWS4" + _o.SecretKey), date), _o.Region), "s3"), "aws4_request"); var signature = Convert.ToHexString(Hmac(signingKey, stringToSign)).ToLowerInvariant();
        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.TryAddWithoutValidation("Authorization", $"AWS4-HMAC-SHA256 Credential={_o.AccessKey}/{scope}, SignedHeaders={signedHeaders}, Signature={signature}");
        if (body is not null) { request.Content = new StreamContent(body); request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType); }
        try { return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct); } catch { request.Dispose(); throw; }
    }
    private static string Escape(string value) => Uri.EscapeDataString(value).Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
    private static string HexSha(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static byte[] Hmac(byte[] key, string value) { using var h = new HMACSHA256(key); return h.ComputeHash(Encoding.UTF8.GetBytes(value)); }
    private static readonly string EmptyHash = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant();
    public void Dispose() { _http.Dispose(); _bucketGate.Dispose(); }
    private sealed class ResponseOwnedStream(Stream inner, HttpResponseMessage response) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => await inner.ReadAsync(buffer, ct);
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                response.Dispose();
            }
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
            response.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
