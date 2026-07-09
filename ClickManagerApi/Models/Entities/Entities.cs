using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClickManagerApi.Models.Entities;

// ── 1. FOTOGRAFO ────────────────────────────────────────────
public class Fotografo
{
    public int    Id           { get; set; }

    [Required, MaxLength(150)]
    public string Nome         { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Email        { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string SenhaHash    { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Telefone    { get; set; }

    [MaxLength(100)]
    public string? Instagram   { get; set; }

    [MaxLength(500)]
    public string? Bio         { get; set; }

    [MaxLength(20)]
    public string PlanoAtivo   { get; set; } = "starter";

    public DateTime CriadoEm     { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public ICollection<Cliente>          Clientes       { get; set; } = [];
    public ICollection<Disponibilidade>  Disponibilidades { get; set; } = [];
    public ICollection<ImagemPortfolio>  Portfolio       { get; set; } = [];
    public ICollection<Ensaio>           Ensaios         { get; set; } = [];
}

// ── 2. DISPONIBILIDADE ──────────────────────────────────────
public class Disponibilidade
{
    public int     Id          { get; set; }
    public int     FotografoId { get; set; }
    public byte    DiaSemana   { get; set; }   // 0=Dom … 6=Sáb
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFim    { get; set; }
    public bool    Ativo       { get; set; } = true;

    public Fotografo? Fotografo { get; set; }
}

// ── 3. CLIENTE ──────────────────────────────────────────────
public class Cliente
{
    public int    Id          { get; set; }
    public int    FotografoId { get; set; }

    [Required, MaxLength(150)]
    public string Nome        { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email      { get; set; }

    [MaxLength(30)]
    public string? Telefone   { get; set; }

    [MaxLength(50)]
    public string? TipoEnsaio { get; set; }

    [MaxLength(20)]
    public string Status      { get; set; } = "Ativo";  // Ativo | Pausado | Inativo

    [MaxLength(500)]
    public string? Observacoes { get; set; }

    public DateTime CriadoEm  { get; set; } = DateTime.UtcNow;

    // Navegação
    public Fotografo?        Fotografo { get; set; }
    public ICollection<Ensaio> Ensaios { get; set; } = [];
}

// ── 4. ENSAIO ───────────────────────────────────────────────
public class Ensaio
{
    public int     Id          { get; set; }
    public int     FotografoId { get; set; }
    public int     ClienteId   { get; set; }

    [Required, MaxLength(200)]
    public string  Titulo      { get; set; } = string.Empty;

    public DateTime DataHora   { get; set; }

    [MaxLength(300)]
    public string? Local       { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Valor       { get; set; }

    [MaxLength(30)]
    public string  Status      { get; set; } = "Agendado"; // Agendado | Realizado | Cancelado

    public int     TotalImagens { get; set; }

    [MaxLength(500)]
    public string? Observacoes  { get; set; }

    /// <summary>
    /// Token opaco (não hasheado) do link público de entrega da galeria ao cliente.
    /// Diferente do token de recuperação de senha, este precisa ser recuperável
    /// (o fotógrafo copia o mesmo link várias vezes), então é guardado em claro —
    /// mesmo modelo de confiança já usado para as URLs de ImagemGaleria/Portfolio,
    /// que também são servidas sem autenticação via wwwroot/uploads.
    /// </summary>
    [MaxLength(64)]
    public string? LinkGaleriaToken { get; set; }
    public bool    LinkGaleriaAtivo { get; set; }

    public DateTime CriadoEm     { get; set; } = DateTime.UtcNow;
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public Fotografo?                Fotografo      { get; set; }
    public Cliente?                  Cliente        { get; set; }
    public Contrato?                 Contrato       { get; set; }
    public ICollection<Pagamento>    Pagamentos     { get; set; } = [];
    public ICollection<ImagemGaleria> ImagensGaleria { get; set; } = [];
}

// ── 5. CONTRATO ─────────────────────────────────────────────
public class Contrato
{
    public int     Id             { get; set; }
    public int     EnsaioId       { get; set; }

    [Required, MaxLength(30)]
    public string  Numero         { get; set; } = string.Empty;

    public string? Conteudo       { get; set; }

    [MaxLength(20)]
    public string  Status         { get; set; } = "Pendente"; // Pendente | Enviado | Assinado | Cancelado

    public DateTime? DataAssinatura { get; set; }
    public DateTime  CriadoEm      { get; set; } = DateTime.UtcNow;

    // Navegação
    public Ensaio? Ensaio { get; set; }
}

// ── 6. PAGAMENTO ────────────────────────────────────────────
public class Pagamento
{
    public int     Id            { get; set; }
    public int     EnsaioId      { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Valor         { get; set; }

    [MaxLength(30)]
    public string  Tipo          { get; set; } = "Total";    // Total | Sinal | Parcela

    [MaxLength(30)]
    public string? Metodo        { get; set; }               // Pix | Cartão | Dinheiro

    [MaxLength(20)]
    public string  Status        { get; set; } = "Pendente"; // Pendente | Pago | Cancelado

    public DateTime? DataPagamento { get; set; }

    [MaxLength(300)]
    public string? Observacoes   { get; set; }

    public DateTime CriadoEm     { get; set; } = DateTime.UtcNow;

    // Navegação
    public Ensaio? Ensaio { get; set; }
}

// ── 7. IMAGEM GALERIA ───────────────────────────────────────
public class ImagemGaleria
{
    public int    Id           { get; set; }
    public int    EnsaioId     { get; set; }

    [Required, MaxLength(300)]
    public string NomeArquivo  { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Url          { get; set; } = string.Empty;

    public int    Ordem        { get; set; }
    public long?  TamanhoBytes { get; set; }
    public DateTime CriadoEm  { get; set; } = DateTime.UtcNow;

    // Navegação
    public Ensaio? Ensaio { get; set; }
}

// ── 8. IMAGEM PORTFÓLIO ─────────────────────────────────────
public class ImagemPortfolio
{
    public int    Id           { get; set; }
    public int    FotografoId  { get; set; }

    [Required, MaxLength(300)]
    public string NomeArquivo  { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Url          { get; set; } = string.Empty;

    public int    Ordem        { get; set; }
    public long?  TamanhoBytes { get; set; }
    public DateTime CriadoEm  { get; set; } = DateTime.UtcNow;

    // Navegação
    public Fotografo? Fotografo { get; set; }
}

// ── 9. TOKEN DE RECUPERAÇÃO DE SENHA ────────────────────────
public class TokenRecuperacaoSenha
{
    public int    Id          { get; set; }
    public int    FotografoId { get; set; }

    /// <summary>
    /// Hash SHA-256 do token. O token em claro só existe no e-mail enviado —
    /// se o banco vazar, os tokens não podem ser reutilizados.
    /// </summary>
    [Required, MaxLength(64)]
    public string TokenHash   { get; set; } = string.Empty;

    public DateTime  ExpiraEm { get; set; }
    public DateTime? UsadoEm  { get; set; }
    public DateTime  CriadoEm { get; set; } = DateTime.UtcNow;

    // Navegação
    public Fotografo? Fotografo { get; set; }
}

// ── 10. LEAD (formulário landing page) ──────────────────────
public class Lead
{
    public int    Id           { get; set; }

    [Required, MaxLength(150)]
    public string Nome         { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Email        { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Telefone    { get; set; }

    [MaxLength(30)]
    public string? Plano       { get; set; }

    [MaxLength(500)]
    public string? Mensagem    { get; set; }

    public bool   EmailEnviado { get; set; } = false;
    public DateTime CriadoEm  { get; set; } = DateTime.UtcNow;
}
