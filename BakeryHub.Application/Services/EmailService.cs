using BakeryHub.Application.Interfaces;
using BakeryHub.Application.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace BakeryHub.Application.Services;

public class EmailService : IEmailService
{
    private readonly MailSettings _mailSettings;

    public EmailService(IOptions<MailSettings> mailSettings)
    {
        _mailSettings = mailSettings.Value;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string content)
    {
        var fromAddress = new MailAddress(_mailSettings.Mail, _mailSettings.DisplayName);
        var toAddress = new MailAddress(toEmail);

        var smtp = new SmtpClient
        {
            Host = _mailSettings.Host,
            Port = _mailSettings.Port,
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(fromAddress.Address, _mailSettings.Password)
        };

        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = subject,
            Body = content,
            IsBodyHtml = true
        };

        await smtp.SendMailAsync(message);
    }
}
