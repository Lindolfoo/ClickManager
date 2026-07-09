using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClickManagerApi.Models.Entities;
using Microsoft.IdentityModel.Tokens;

namespace ClickManagerApi.Services;

public interface IAuthService
{
    string GenerateToken(Fotografo fotografo);
}

public class AuthService : IAuthService
{
    private readonly IConfiguration _config;

    public AuthService(IConfiguration config) => _config = config;

    public string GenerateToken(Fotografo fotografo)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var hours = int.TryParse(_config["Jwt:ExpirationHours"], out var h) ? h : 24;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, fotografo.Id.ToString()),
            new Claim(ClaimTypes.Email,           fotografo.Email),
            new Claim(ClaimTypes.Name,            fotografo.Nome)
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(hours),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
