using ClickManagerApi.Models;
using ClickManagerApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClickManagerApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContactController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<ContactController> _logger;

    public ContactController(IEmailService emailService, ILogger<ContactController> logger)
    {
        _emailService = emailService;
        _logger       = logger;
    }

    // POST /api/contact
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ContactRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 3)
            return BadRequest(new { Success = false, Message = "Nome invalido." });

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return BadRequest(new { Success = false, Message = "E-mail invalido." });

        if (string.IsNullOrWhiteSpace(request.Plan))
            return BadRequest(new { Success = false, Message = "Selecione um plano." });

        try
        {
            await _emailService.SendContactEmailsAsync(request);
            _logger.LogInformation("Contato de {Name} processado.", request.Name);
            return Ok(new { Success = true, Message = "Solicitacao recebida! Enviamos uma confirmacao para " + request.Email });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar e-mail.");
            return StatusCode(500, new { Success = false, Message = "Erro ao enviar e-mail. Tente novamente." });
        }
    }

    // GET /api/contact/health
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { Status = "online", Timestamp = DateTime.Now });
}
