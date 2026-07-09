# Click Manager – Modelagem de Banco de Dados (SQL Server)

## Visão geral do modelo

```
Fotografo (1) ──< Cliente (N)
Cliente   (1) ──< Ensaio (N)
Ensaio    (1) ──< Contrato (1)
Ensaio    (1) ──< Pagamento (N)
Ensaio    (1) ──< ImagemGaleria (N)
Fotografo (1) ──< ImagemPortfolio (N)
Leads     (independente – formulário do site)
```

---

## Script SQL Server completo

```sql
-- ============================================================
--  Click Manager – SQL Server
--  Criação do banco e todas as tabelas
-- ============================================================

CREATE DATABASE ClickManagerDB;
GO

USE ClickManagerDB;
GO

-- ------------------------------------------------------------
-- 1. FOTOGRAFO  (usuário do sistema)
-- ------------------------------------------------------------
CREATE TABLE Fotografo (
    Id            INT           IDENTITY(1,1) PRIMARY KEY,
    Nome          NVARCHAR(150) NOT NULL,
    Email         NVARCHAR(200) NOT NULL UNIQUE,
    SenhaHash     NVARCHAR(500) NOT NULL,       -- bcrypt hash
    Telefone      NVARCHAR(20)  NULL,
    Instagram     NVARCHAR(100) NULL,
    Bio           NVARCHAR(500) NULL,
    PlanoAtivo    NVARCHAR(20)  NOT NULL DEFAULT 'starter',
                                                -- starter | pro | studio
    CriadoEm     DATETIME2     NOT NULL DEFAULT GETDATE(),
    AtualizadoEm DATETIME2     NOT NULL DEFAULT GETDATE()
);

-- ------------------------------------------------------------
-- 2. DISPONIBILIDADE  (dias/horários de trabalho do fotógrafo)
-- ------------------------------------------------------------
CREATE TABLE Disponibilidade (
    Id           INT          IDENTITY(1,1) PRIMARY KEY,
    FotografoId  INT          NOT NULL REFERENCES Fotografo(Id) ON DELETE CASCADE,
    DiaSemana    TINYINT      NOT NULL,   -- 0=Dom, 1=Seg, ... 6=Sáb
    HoraInicio   TIME         NOT NULL,
    HoraFim      TIME         NOT NULL,
    Ativo        BIT          NOT NULL DEFAULT 1
);

-- ------------------------------------------------------------
-- 3. CLIENTE
-- ------------------------------------------------------------
CREATE TABLE Cliente (
    Id            INT           IDENTITY(1,1) PRIMARY KEY,
    FotografoId   INT           NOT NULL REFERENCES Fotografo(Id) ON DELETE CASCADE,
    Nome          NVARCHAR(150) NOT NULL,
    Email         NVARCHAR(200) NULL,
    Telefone      NVARCHAR(30)  NULL,
    TipoEnsaio    NVARCHAR(50)  NULL,    -- Casamento, Newborn, Infantil…
    Status        NVARCHAR(20)  NOT NULL DEFAULT 'Ativo',  -- Ativo | Pausado | Inativo
    Observacoes   NVARCHAR(500) NULL,
    CriadoEm     DATETIME2     NOT NULL DEFAULT GETDATE()
);

-- ------------------------------------------------------------
-- 4. ENSAIO
-- ------------------------------------------------------------
CREATE TABLE Ensaio (
    Id            INT            IDENTITY(1,1) PRIMARY KEY,
    FotografoId   INT            NOT NULL REFERENCES Fotografo(Id),
    ClienteId     INT            NOT NULL REFERENCES Cliente(Id),
    Titulo        NVARCHAR(200)  NOT NULL,
    DataHora      DATETIME2      NOT NULL,
    Local         NVARCHAR(300)  NULL,
    Valor         DECIMAL(10,2)  NOT NULL DEFAULT 0,
    Status        NVARCHAR(30)   NOT NULL DEFAULT 'Agendado',
                                          -- Agendado | Realizado | Cancelado
    TotalImagens  INT            NOT NULL DEFAULT 0,
    Observacoes   NVARCHAR(500)  NULL,
    CriadoEm     DATETIME2      NOT NULL DEFAULT GETDATE(),
    AtualizadoEm DATETIME2      NOT NULL DEFAULT GETDATE()
);

-- ------------------------------------------------------------
-- 5. CONTRATO
-- ------------------------------------------------------------
CREATE TABLE Contrato (
    Id             INT           IDENTITY(1,1) PRIMARY KEY,
    EnsaioId       INT           NOT NULL UNIQUE REFERENCES Ensaio(Id),
    Numero         NVARCHAR(30)  NOT NULL UNIQUE,   -- ex: CTR16778
    Conteudo       NVARCHAR(MAX) NULL,               -- texto/HTML do contrato
    Status         NVARCHAR(20)  NOT NULL DEFAULT 'Pendente',
                                          -- Pendente | Enviado | Assinado | Cancelado
    DataAssinatura DATETIME2     NULL,
    CriadoEm      DATETIME2     NOT NULL DEFAULT GETDATE()
);

-- Gera número de contrato automaticamente (trigger)
GO
CREATE TRIGGER trg_Contrato_Numero
ON Contrato AFTER INSERT
AS
BEGIN
    UPDATE Contrato
    SET Numero = 'CTR' + RIGHT('00000' + CAST(Id AS VARCHAR), 5)
    WHERE Id IN (SELECT Id FROM inserted) AND Numero = '';
END;
GO

-- ------------------------------------------------------------
-- 6. PAGAMENTO
-- ------------------------------------------------------------
CREATE TABLE Pagamento (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    EnsaioId     INT           NOT NULL REFERENCES Ensaio(Id),
    Valor        DECIMAL(10,2) NOT NULL,
    Tipo         NVARCHAR(30)  NOT NULL DEFAULT 'Total',   -- Total | Sinal | Parcela
    Metodo       NVARCHAR(30)  NULL,                       -- Pix | Cartão | Dinheiro
    Status       NVARCHAR(20)  NOT NULL DEFAULT 'Pendente',-- Pendente | Pago | Cancelado
    DataPagamento DATETIME2    NULL,
    Observacoes  NVARCHAR(300) NULL,
    CriadoEm    DATETIME2     NOT NULL DEFAULT GETDATE()
);

-- ------------------------------------------------------------
-- 7. IMAGEM_GALERIA  (fotos entregues por ensaio)
-- ------------------------------------------------------------
CREATE TABLE ImagemGaleria (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    EnsaioId     INT           NOT NULL REFERENCES Ensaio(Id) ON DELETE CASCADE,
    NomeArquivo  NVARCHAR(300) NOT NULL,
    Url          NVARCHAR(500) NOT NULL,
    Ordem        INT           NOT NULL DEFAULT 0,
    TamanhoBytes BIGINT        NULL,
    CriadoEm    DATETIME2     NOT NULL DEFAULT GETDATE()
);

-- ------------------------------------------------------------
-- 8. PORTFOLIO  (imagens públicas do fotógrafo)
-- ------------------------------------------------------------
CREATE TABLE ImagemPortfolio (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    FotografoId  INT           NOT NULL REFERENCES Fotografo(Id) ON DELETE CASCADE,
    NomeArquivo  NVARCHAR(300) NOT NULL,
    Url          NVARCHAR(500) NOT NULL,
    Ordem        INT           NOT NULL DEFAULT 0,
    TamanhoBytes BIGINT        NULL,
    CriadoEm    DATETIME2     NOT NULL DEFAULT GETDATE()
);

-- ------------------------------------------------------------
-- 9. LEAD  (formulário do site / landing page)
-- ------------------------------------------------------------
CREATE TABLE Lead (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    Nome         NVARCHAR(150) NOT NULL,
    Email        NVARCHAR(200) NOT NULL,
    Telefone     NVARCHAR(30)  NULL,
    Plano        NVARCHAR(30)  NULL,
    Mensagem     NVARCHAR(500) NULL,
    EmailEnviado BIT           NOT NULL DEFAULT 0,
    CriadoEm    DATETIME2     NOT NULL DEFAULT GETDATE()
);
GO
```

---

## Relacionamentos (resumo)

| Tabela | FK | Cardinalidade |
|---|---|---|
| Cliente | FotografoId → Fotografo | N:1 |
| Disponibilidade | FotografoId → Fotografo | N:1 |
| Ensaio | FotografoId → Fotografo | N:1 |
| Ensaio | ClienteId → Cliente | N:1 |
| Contrato | EnsaioId → Ensaio | 1:1 |
| Pagamento | EnsaioId → Ensaio | N:1 |
| ImagemGaleria | EnsaioId → Ensaio | N:1 |
| ImagemPortfolio | FotografoId → Fotografo | N:1 |

---

## Índices recomendados

```sql
CREATE INDEX IX_Cliente_FotografoId   ON Cliente(FotografoId);
CREATE INDEX IX_Ensaio_FotografoId    ON Ensaio(FotografoId);
CREATE INDEX IX_Ensaio_ClienteId      ON Ensaio(ClienteId);
CREATE INDEX IX_Ensaio_DataHora       ON Ensaio(DataHora);
CREATE INDEX IX_Pagamento_EnsaioId    ON Pagamento(EnsaioId);
CREATE INDEX IX_ImagemGaleria_EnsaioId ON ImagemGaleria(EnsaioId);
CREATE INDEX IX_ImagemPortfolio_FotId  ON ImagemPortfolio(FotografoId);
```
