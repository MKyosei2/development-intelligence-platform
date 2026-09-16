// Purpose: Defines QueuedMediaUpload.
using System.Text.Json;

namespace Dip.Agent.Core;

public sealed record QueuedMediaUpload(
    string Id,
    string EventId,
    ProductionToolSource Source,
    ProductionMediaKind Kind,
    string RelativePath,
    string SpoolPath,
    string ContentType,
    string Sha256,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc,
    int Attempts);
