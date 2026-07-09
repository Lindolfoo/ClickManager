namespace ClickManagerApi.Models;

public record LoginRequest(string Email, string Senha);

public record RegisterRequest(
    string Nome,
    string Email,
    string Senha,
    string? Telefone = null,
    string PlanoAtivo = "starter"
);

public class AuthResponse
{
    public required string Token      { get; init; }
    public DateTime        ExpiresAt  { get; init; }
    public required FotografoDto Fotografo { get; init; }
}

public class FotografoDto
{
    public int     Id         { get; init; }
    public required string Nome       { get; init; }
    public required string Email      { get; init; }
    public string? Telefone   { get; init; }
    public string? Instagram  { get; init; }
    public string? Bio        { get; init; }
    public required string PlanoAtivo { get; init; }
}

public record UpdateProfileRequest(
    string Nome,
    string? Telefone = null,
    string? Instagram = null,
    string? Bio = null
);

/// <summary>Solicita o envio do link de redefinição de senha.</summary>
public record ForgotPasswordRequest(string Email);

/// <summary>Redefine a senha usando o token recebido por e-mail.</summary>
public record ResetPasswordRequest(string Token, string NovaSenha);

/// <summary>Confirma a senha atual antes de apagar a conta e todos os dados (LGPD).</summary>
public record DeleteAccountRequest(string Senha);
