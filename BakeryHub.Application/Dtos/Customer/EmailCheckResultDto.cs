namespace BakeryHub.Application.Dtos;
public class EmailCheckResultDto
{
    public bool Exists { get; set; }
    public bool IsAdmin { get; set; } = false;
    public bool IsCustomer { get; set; } = false;
    public string? Name { get; set; }
}
