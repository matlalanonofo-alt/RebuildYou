using System.Net;
using System.Net.Mail;

namespace RebuildYou.Web.Services;

public interface IAppEmailSender
{
    bool IsConfigured { get; }
    Task SendPasswordResetAsync(string email, string resetUrl);
}

public sealed class SmtpEmailSender(IConfiguration configuration) : IAppEmailSender
{
    readonly IConfigurationSection settings = configuration.GetSection("Email");

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(settings["Host"]) &&
        int.TryParse(settings["Port"], out var port) && port is > 0 and <= 65535 &&
        !string.IsNullOrWhiteSpace(settings["Username"]) &&
        !string.IsNullOrWhiteSpace(settings["Password"]) &&
        MailAddress.TryCreate(settings["FromEmail"], out _);

    public async Task SendPasswordResetAsync(string email, string resetUrl)
    {
        if (!IsConfigured) throw new InvalidOperationException("Password reset email is not configured.");

        var host = settings["Host"]!;
        var port = int.Parse(settings["Port"]!);
        var fromEmail = settings["FromEmail"]!;
        var fromName = settings["FromName"] ?? "RebuildYOU Training Centre";
        var username = settings["Username"];
        var password = settings["Password"];

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = "Reset your RebuildYOU password",
            Body = $"<p>We received a request to reset the password for your RebuildYOU account.</p><p><a href=\"{WebUtility.HtmlEncode(resetUrl)}\">Reset your password</a></p><p>This link expires in one hour. If you did not request a reset, you can ignore this email.</p>",
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(email));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = bool.TryParse(settings["EnableSsl"], out var enableSsl) && enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password ?? "");
        await client.SendMailAsync(message);
    }
}
