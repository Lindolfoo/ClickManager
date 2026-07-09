namespace ClickManagerApi.Models;

public class ContactRequest
{
    public string  Name    { get; set; } = string.Empty;
    public string  Email   { get; set; } = string.Empty;
    public string? Phone   { get; set; }
    public string  Plan    { get; set; } = string.Empty;
    public string? Message { get; set; }
}

public class ApiResponse
{
    public bool   Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
