// Purpose: Represents I nt eg ra ti on Co nn ec ti on in the Integrations domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class IntegrationConnection
{
    private IntegrationConnection() { }

    public IntegrationConnection(
        Guid id,
        string provider,
        string displayName,
        IntegrationConnectionMode mode,
        string? baseUrl,
        string credentialReference,
        DateTimeOffset now,
        string? externalAccount = null)
    {
        if (!Enum.IsDefined(mode) || mode == IntegrationConnectionMode.Unknown)
            throw new ArgumentOutOfRangeException(nameof(mode));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Provider = Required(provider, 80, nameof(provider));
        DisplayName = Required(displayName, 200, nameof(displayName));
        Mode = mode;
        BaseUrl = Optional(baseUrl, 1000, nameof(baseUrl));
        CredentialReference = Required(credentialReference, 1000, nameof(credentialReference));
        ExternalAccount = Optional(externalAccount, 300, nameof(externalAccount));
        Enabled = true;
        HealthState = ConnectionHealthState.Unknown;
        CreatedAtUtc = UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public string Provider { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public IntegrationConnectionMode Mode { get; private set; }
    public string? BaseUrl { get; private set; }
    public string CredentialReference { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public string? ExternalAccount { get; private set; }
    public ConnectionHealthState HealthState { get; private set; }
    public string? LastHealthMessage { get; private set; }
    public DateTimeOffset? LastTestedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void SetEnabled(bool enabled, DateTimeOffset now)
    {
        Enabled = enabled;
        UpdatedAtUtc = now;
        if (!enabled)
        {
            HealthState = ConnectionHealthState.Disabled;
            LastHealthMessage = "Connection is disabled.";
        }
        else if (HealthState == ConnectionHealthState.Disabled)
        {
            HealthState = ConnectionHealthState.Unknown;
            LastHealthMessage = null;
        }
    }

    public void RecordHealth(
        ConnectionHealthState state,
        string message,
        string? externalAccount,
        DateTimeOffset now)
    {
        if (!Enum.IsDefined(state))
            throw new ArgumentOutOfRangeException(nameof(state));
        HealthState = Enabled ? state : ConnectionHealthState.Disabled;
        LastHealthMessage = Required(message, 2000, nameof(message));
        ExternalAccount = Optional(externalAccount, 300, nameof(externalAccount));
        LastTestedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void ReplaceCredentialReference(string credentialReference, DateTimeOffset now)
    {
        CredentialReference = Required(credentialReference, 1000, nameof(credentialReference));
        UpdatedAtUtc = now;
        HealthState = ConnectionHealthState.Unknown;
        LastHealthMessage = "Credentials were updated and should be re-tested.";
    }

    private static string Required(string? value, int max, string name)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > max)
            throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return normalized;
    }

    private static string? Optional(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length > max)
            throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return normalized;
    }
}
