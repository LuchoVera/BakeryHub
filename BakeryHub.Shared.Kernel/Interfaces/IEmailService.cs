namespace BakeryHub.Shared.Kernel.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string content);
}
