namespace BakeryHub.Application.Models;

public class MailSettings
{
    public required string Mail { get; set; }
    public required string DisplayName { get; set; }
    public required string Password { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; }
}
