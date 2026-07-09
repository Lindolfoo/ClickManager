using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClickManagerApi.Services;

/// <summary>
/// Captura qualquer exceção não tratada e devolve um ProblemDetails (RFC 7807)
/// genérico, sem vazar stack trace ou detalhes internos para o cliente.
/// A exceção completa fica registrada no log do servidor.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception,
            "Exceção não tratada em {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Status   = StatusCodes.Status500InternalServerError,
            Title    = "Erro interno no servidor.",
            Detail   = "Ocorreu um erro inesperado. Tente novamente mais tarde.",
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true; // exceção tratada — não propaga
    }
}
