using ClickManagerApi.Models;

namespace ClickManagerApi.Services;

public interface IEmailService
{
    Task SendContactEmailsAsync(ContactRequest request);

    /// <summary>
    /// Envia o e-mail com o link de redefinição de senha.
    /// O link contém o token em claro — que nunca é persistido no banco.
    /// </summary>
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);
}
