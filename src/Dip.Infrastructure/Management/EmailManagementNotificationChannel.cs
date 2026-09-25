// Purpose: Implements Email Management Notification Channel infrastructure responsibilities for management.
using System.Net;
using System.Net.Mail;
using Dip.Application.Features.Management;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Management;
public sealed class EmailManagementNotificationChannel : IManagementNotificationChannel
{
    private readonly ManagementNotificationOptions _all;
    private readonly ManagementNotificationOptions.EmailOptions _o;
    public string ChannelName => "Email";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.Host) && !string.IsNullOrWhiteSpace(_o.From) && (!string.IsNullOrWhiteSpace(_o.To) || _all.RoleDestinations.Values.Any(x => !string.IsNullOrWhiteSpace(x.EmailTo)));
    public EmailManagementNotificationChannel(IOptions<ManagementNotificationOptions> options) { _all = options.Value; _o = _all.Email; }
    public async Task DeliverAsync(ManagementNotificationEnvelope m, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Management notification email is not configured.");
        var recipient = _all.DestinationFor(m.OperationalAlert.RoutedRole)?.EmailTo;
        if (string.IsNullOrWhiteSpace(recipient)) recipient = _o.To;
        if (string.IsNullOrWhiteSpace(recipient)) throw new InvalidOperationException($"Management notification email recipient is not configured for role '{m.OperationalAlert.RoutedRole}'.");
        using var message = new MailMessage { From = new MailAddress(_o.From), Subject = m.Subject, Body = m.Body, IsBodyHtml = false };
        foreach (var to in recipient.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) message.To.Add(to);
        using var smtp = new SmtpClient(_o.Host, Math.Clamp(_o.Port, 1, 65535)) { EnableSsl = _o.EnableSsl };
        if (!string.IsNullOrWhiteSpace(_o.Username)) smtp.Credentials = new NetworkCredential(_o.Username, _o.Password);
        ct.ThrowIfCancellationRequested();
        await smtp.SendMailAsync(message);
        ct.ThrowIfCancellationRequested();
    }
}
