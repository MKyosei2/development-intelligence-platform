// Purpose: Provides infrastructure behavior for this application boundary.
namespace Dip.Infrastructure.Management;

public sealed class ManagementNotificationOptions
{
    public const string SectionName = "ManagementNotifications";
    public bool Enabled { get; set; } = true;
    public int ScanIntervalSeconds { get; set; } = 60;
    public string SlackWebhookUrl { get; set; } = string.Empty;
    public string TeamsWebhookUrl { get; set; } = string.Empty;
    public Dictionary<string, string[]> RoleRoutes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, RoleDestinationOptions> RoleDestinations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public EmailOptions Email { get; set; } = new();
    public RoleDestinationOptions? DestinationFor(string role)
    {
        if (!string.IsNullOrWhiteSpace(role) && RoleDestinations.TryGetValue(role, out var direct)) return direct;
        return RoleDestinations.TryGetValue("*", out var fallback) ? fallback : null;
    }
    public sealed class RoleDestinationOptions
    {
        public string SlackWebhookUrl { get; set; } = string.Empty; public string TeamsWebhookUrl { get; set; } = string.Empty; public string EmailTo { get; set; } = string.Empty;
    }
    public sealed class EmailOptions
    {
        public string Host { get; set; } = string.Empty; public int Port { get; set; } = 587; public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = string.Empty; public string Password { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty; public string To { get; set; } = string.Empty;
    }
}
