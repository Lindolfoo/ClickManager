using ClickManagerApi.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ClickManagerApi.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task SendContactEmailsAsync(ContactRequest request)
    {
        await Task.WhenAll(
            SendConfirmationToUserAsync(request),
            SendNotificationToAdminAsync(request)
        );
    }

    private async Task SendConfirmationToUserAsync(ContactRequest request)
    {
        var html = BuildConfirmationHtml(request);

        var m = new MimeMessage();
        m.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        m.To.Add(new MailboxAddress(request.Name, request.Email));
        m.Subject = "Recebemos sua solicitacao - Click Manager";
        m.Body    = new BodyBuilder { HtmlBody = html }.ToMessageBody();
        await SendAsync(m);
        _logger.LogInformation("Confirmacao enviada para {Email}", request.Email);
    }

    private async Task SendNotificationToAdminAsync(ContactRequest request)
    {
        var html = BuildAdminNotificationHtml(request);

        var m = new MimeMessage();
        m.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        m.To.Add(new MailboxAddress("Admin", _settings.AdminEmail));
        m.Subject = "Novo lead: " + request.Name + " - " + GetPlanLabel(request.Plan);
        m.Body    = new BodyBuilder { HtmlBody = html }.ToMessageBody();
        await SendAsync(m);
        _logger.LogInformation("Notificacao admin enviada.");
    }

    // Público e estático: strings puras, sem dependência de SMTP, então dá pra
    // testar a codificação HTML (campos vêm de um endpoint público, sem auth)
    // sem precisar simular o envio de e-mail.
    public static string BuildConfirmationHtml(ContactRequest request)
    {
        var planLabel = GetPlanLabel(request.Plan);
        var name    = System.Net.WebUtility.HtmlEncode(request.Name);
        var email   = System.Net.WebUtility.HtmlEncode(request.Email);
        var phoneEnc = string.IsNullOrEmpty(request.Phone)   ? null : System.Net.WebUtility.HtmlEncode(request.Phone);
        var messageEnc = string.IsNullOrEmpty(request.Message) ? null : System.Net.WebUtility.HtmlEncode(request.Message);
        var phone = phoneEnc is null ? "" : "<p><strong>WhatsApp:</strong> " + phoneEnc + "</p>";
        var extra = messageEnc is null ? "" : "<p><strong>Mensagem:</strong> " + messageEnc + "</p>";

        return
            "<!DOCTYPE html><html><head><meta charset='UTF-8'/><style>" +
            "body{font-family:Arial,sans-serif;background:#f5f3fb;margin:0}" +
            ".w{max-width:560px;margin:40px auto;background:#fff;border-radius:16px;overflow:hidden}" +
            ".h{background:#7c3aed;padding:32px 40px;text-align:center}" +
            ".h h1{color:#fff;margin:0}" +
            ".h p{color:#ddd6fe;margin:4px 0 0}" +
            ".b{padding:32px 40px;color:#1a1625}" +
            ".b h2{color:#7c3aed}" +
            ".box{background:#ede9f7;border-radius:10px;padding:16px 20px;margin:20px 0}" +
            ".box p{margin:4px 0}" +
            ".box strong{color:#5b21b6}" +
            ".f{background:#ede9f7;padding:16px 40px;text-align:center;font-size:.8rem;color:#6b6480}" +
            "</style></head><body><div class='w'>" +
            "<div class='h'><h1>Click Manager</h1><p>Gestao para fotografos</p></div>" +
            "<div class='b'><h2>Ola, " + name + "!</h2>" +
            "<p>Recebemos sua solicitacao. Entraremos em contato em breve.</p>" +
            "<div class='box'>" +
            "<p><strong>Plano:</strong> " + planLabel + "</p>" +
            "<p><strong>E-mail:</strong> " + email + "</p>" +
            phone + extra + "</div></div>" +
            "<div class='f'>2026 Click Manager - PIM III - UNIP ADS</div>" +
            "</div></body></html>";
    }

    public static string BuildAdminNotificationHtml(ContactRequest request)
    {
        var planLabel = GetPlanLabel(request.Plan);
        var name    = System.Net.WebUtility.HtmlEncode(request.Name);
        var email   = System.Net.WebUtility.HtmlEncode(request.Email);
        var phone   = System.Net.WebUtility.HtmlEncode(request.Phone   ?? "-");
        var message = System.Net.WebUtility.HtmlEncode(request.Message ?? "-");

        return
            "<!DOCTYPE html><html><head><meta charset='UTF-8'/><style>" +
            "body{font-family:Arial,sans-serif;background:#f5f3fb}" +
            ".c{max-width:500px;margin:30px auto;background:#fff;border-radius:12px;padding:28px}" +
            "h2{color:#7c3aed;margin-top:0}" +
            "table{width:100%;border-collapse:collapse}" +
            "td{padding:8px 12px;border-bottom:1px solid #ede9f7}" +
            "td:first-child{font-weight:600;color:#5b21b6;width:120px}" +
            "</style></head><body><div class='c'><h2>Novo lead</h2><table>" +
            "<tr><td>Nome</td><td>"     + name                                      + "</td></tr>" +
            "<tr><td>E-mail</td><td>"   + email                                     + "</td></tr>" +
            "<tr><td>WhatsApp</td><td>" + phone                                     + "</td></tr>" +
            "<tr><td>Plano</td><td>"    + planLabel                                 + "</td></tr>" +
            "<tr><td>Mensagem</td><td>" + message                                   + "</td></tr>" +
            "<tr><td>Data</td><td>"     + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "</td></tr>" +
            "</table></div></body></html>";
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
    {
        // HtmlEncode: o nome vem do cadastro do usuário e é interpolado em HTML.
        var nome = System.Net.WebUtility.HtmlEncode(toName);
        var link = System.Net.WebUtility.HtmlEncode(resetLink);

        var html =
            "<!DOCTYPE html><html><head><meta charset='UTF-8'/><style>" +
            "body{font-family:Arial,sans-serif;background:#f5f3fb;margin:0}" +
            ".w{max-width:560px;margin:40px auto;background:#fff;border-radius:16px;overflow:hidden}" +
            ".h{background:#7c3aed;padding:32px 40px;text-align:center}" +
            ".h h1{color:#fff;margin:0}" +
            ".b{padding:32px 40px;color:#1a1625;line-height:1.6}" +
            ".b h2{color:#7c3aed}" +
            ".btn{display:inline-block;background:#7c3aed;color:#fff;text-decoration:none;" +
            "padding:14px 28px;border-radius:8px;font-weight:600;margin:20px 0}" +
            ".small{font-size:.8rem;color:#6b6480;word-break:break-all}" +
            ".f{background:#ede9f7;padding:16px 40px;text-align:center;font-size:.8rem;color:#6b6480}" +
            "</style></head><body><div class='w'>" +
            "<div class='h'><h1>Click Manager</h1></div>" +
            "<div class='b'><h2>Ola, " + nome + "!</h2>" +
            "<p>Recebemos um pedido para redefinir a senha da sua conta. " +
            "Clique no botao abaixo para criar uma nova senha:</p>" +
            "<p style='text-align:center'><a class='btn' href='" + link + "'>Redefinir minha senha</a></p>" +
            "<p><strong>Este link expira em 1 hora</strong> e so pode ser usado uma vez.</p>" +
            "<p>Se voce nao solicitou a redefinicao, ignore este e-mail. " +
            "Sua senha atual continua valida.</p>" +
            "<p class='small'>Se o botao nao funcionar, copie e cole este endereco no navegador:<br/>" + link + "</p>" +
            "</div><div class='f'>Click Manager - Gestao para fotografos</div>" +
            "</div></body></html>";

        var m = new MimeMessage();
        m.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
        m.To.Add(new MailboxAddress(toName, toEmail));
        m.Subject = "Redefinicao de senha - Click Manager";
        m.Body    = new BodyBuilder { HtmlBody = html }.ToMessageBody();
        await SendAsync(m);

        // Nunca logar o token/link — apenas o destinatário.
        _logger.LogInformation("E-mail de redefinicao de senha enviado para {Email}", toEmail);
    }

    private async Task SendAsync(MimeMessage message)
    {
        using var client = new SmtpClient();
        await client.ConnectAsync(
            _settings.SmtpHost, _settings.SmtpPort,
            _settings.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(_settings.SenderEmail, _settings.SenderPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    private static string GetPlanLabel(string plan) => plan switch
    {
        "starter" => "Starter (Gratis)",
        "pro"     => "Pro (R$ 49/mes)",
        "studio"  => "Studio (R$ 99/mes)",
        _         => plan
    };
}
