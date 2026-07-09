namespace ClickManagerApi.Models;

public class EmailSettings
{
    public string SmtpHost       { get; set; } = string.Empty;
    public int    SmtpPort       { get; set; } = 587;
    public bool   UseSsl         { get; set; } = false;
    public string SenderName     { get; set; } = string.Empty;
    public string SenderEmail    { get; set; } = string.Empty;
    public string SenderPassword { get; set; } = string.Empty;
    public string AdminEmail     { get; set; } = string.Empty;
}
