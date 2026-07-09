# Click Manager

[![CI](https://github.com/SEU_USUARIO/ClickManager/actions/workflows/ci.yml/badge.svg)](https://github.com/SEU_USUARIO/ClickManager/actions/workflows/ci.yml)

Sistema de gestão para fotógrafos, com front-end estático e back-end ASP.NET Core. O projeto cobre o fluxo de cadastro/login, painel operacional, clientes, ensaios, contratos, pagamentos, galeria, portfólio e formulário público de contato.

## Visão Geral

O repositório está dividido em três partes principais:

- `clickmanager-v2/`: front-end em HTML, CSS e JavaScript puro.
- `ClickManagerApi/`: API REST em ASP.NET Core 8 com autenticação JWT, EF Core, PostgreSQL, upload de imagens e envio de e-mails.
- `ClickManagerApi.Tests/`: testes automatizados funcionais, de segurança, robustez e performance usando xUnit e `WebApplicationFactory`.

## Funcionalidades

- Cadastro e login de fotógrafos com senha criptografada por BCrypt.
- Autenticação por JWT e isolamento dos dados por fotógrafo autenticado.
- Dashboard com próximos ensaios, clientes ativos, pagamentos pendentes, total recebido e agenda da semana.
- CRUD de clientes.
- CRUD de ensaios com criação automática de contrato.
- Gestão de contratos com upsert de conteúdo e assinatura.
- Gestão de pagamentos e confirmação de pagamento.
- Upload de imagens para galeria de ensaios.
- Portfólio público por fotógrafo e gestão autenticada de imagens.
- Formulário público de contato com validação e envio de e-mails via SMTP.
- Migrações aplicadas automaticamente no startup da API.
- Arquivos estáticos e uploads servidos pela própria API.

## Estrutura

```text
.
├── ClickManager.slnx
├── ClickManagerApi/
│   ├── Controllers/
│   ├── Data/
│   ├── Migrations/
│   ├── Models/
│   ├── Services/
│   ├── Program.cs
│   ├── appsettings.json
│   └── ClickManagerApi.csproj
├── ClickManagerApi.Tests/
│   ├── FunctionalTests/
│   ├── Helpers/
│   ├── PerformanceTests.cs
│   ├── RobustnessTests.cs
│   ├── SecurityTests.cs
│   └── ClickManagerApi.Tests.csproj
└── clickmanager-v2/
    ├── app.html
    ├── cadastro.html
    ├── index.html
    ├── login.html
    ├── main.js
    ├── global.css
    ├── img/
    └── icons/
```

## Tecnologias

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8
- PostgreSQL 15+
- JWT Bearer Authentication
- BCrypt.Net-Next
- MailKit
- xUnit
- Microsoft.AspNetCore.Mvc.Testing
- EntityFrameworkCore.InMemory nos testes
- HTML, CSS e JavaScript puro no front-end

## Modelo de Dados

As principais entidades ficam em `ClickManagerApi/Models/Entities/Entities.cs`:

- `Fotografo`: usuário principal do sistema.
- `Cliente`: cliente vinculado a um fotógrafo.
- `Ensaio`: sessão fotográfica vinculada a cliente e fotógrafo.
- `Contrato`: contrato individual de um ensaio.
- `Pagamento`: cobrança/recebimento vinculado a ensaio.
- `ImagemGaleria`: imagens privadas da galeria de um ensaio.
- `ImagemPortfolio`: imagens públicas do portfólio.
- `Disponibilidade`: horários disponíveis do fotógrafo.
- `Lead`: dados do formulário público de contato.

## Configuração

O arquivo `ClickManagerApi/appsettings.json` está sanitizado e usa placeholders. Antes de rodar localmente, ajuste:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=clickmanager;Username=postgres;Password=your_db_password"
  },
  "EmailSettings": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "UseSsl": false,
    "SenderName": "Click Manager",
    "SenderEmail": "sender@example.com",
    "SenderPassword": "your_email_password",
    "AdminEmail": "admin@example.com"
  },
  "Jwt": {
    "Key": "SUA_CHAVE_JWT_COM_PELO_MENOS_32_CARACTERES",
    "Issuer": "ClickManagerApi",
    "Audience": "ClickManagerFrontend",
    "ExpirationHours": 24
  }
}
```

## Como Rodar

Pré-requisitos:

- Docker e Docker Compose (opção 1), **ou**
- .NET 8 SDK + PostgreSQL 15+ (opção 2)
- Navegador moderno

### Opção 1 — Docker (recomendado)

Sobe a API e o PostgreSQL já configurados:

```bash
cp .env.example .env    # preencha POSTGRES_PASSWORD e JWT_KEY
docker compose up --build
```

A API fica em `http://localhost:5000` e as migrations são aplicadas automaticamente.

### Opção 2 — Local

Rodar a API:

```bash
dotnet restore
dotnet run --project ClickManagerApi
```

URLs configuradas em `launchSettings.json`:

- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:7000`

Health check:

```text
GET http://localhost:5000/api/contact/health
```

Rodar o front-end:

- Abra `clickmanager-v2/index.html`, `login.html`, `cadastro.html` ou `app.html` no navegador.
- Para melhor experiência local, use um servidor estático como Live Server na porta `5500`.
- O front-end usa `http://localhost:5000/api` como base da API em `clickmanager-v2/main.js`.

## CORS

As origens permitidas vêm da configuração, não do código. Em `appsettings.json`:

```json
"Cors": {
  "AllowedOrigins": [
    "http://127.0.0.1:5500",
    "http://localhost:5500",
    "http://localhost:3000"
  ]
}
```

Em produção, defina o domínio real via variável de ambiente `Cors__AllowedOrigins__0`.

## Segurança

- Senhas com hash BCrypt; recuperação de senha por token de uso único (SHA-256 no banco, expira em 1 hora).
- Rate limiting: 100 req/min por IP no geral e 10 tentativas/15 min nos endpoints de autenticação.
- Security headers em todas as respostas: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy` e `Permissions-Policy`.
- HSTS e redirecionamento HTTPS ativos fora do ambiente de desenvolvimento.
- Isolamento multi-tenant: toda consulta filtra pelo `FotografoId` extraído do JWT.
- Erros não tratados retornam `ProblemDetails` genérico; o stack trace fica só no log do servidor.

## Endpoints

Rotas públicas:

| Método | Rota | Descrição |
| --- | --- | --- |
| `POST` | `/api/auth/register` | Cadastra fotógrafo e retorna token JWT. |
| `POST` | `/api/auth/login` | Autentica fotógrafo e retorna token JWT. |
| `POST` | `/api/auth/forgot-password` | Envia link de redefinição por e-mail (resposta sempre genérica). |
| `POST` | `/api/auth/reset-password` | Redefine a senha usando o token recebido por e-mail. |
| `POST` | `/api/contact` | Recebe formulário público e envia e-mails. |
| `GET` | `/api/contact/health` | Verifica status da API. |
| `GET` | `/api/portfolio?fotografoId=1` | Lista portfólio público de um fotógrafo. |

Rotas autenticadas:

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/api/auth/me` | Retorna perfil do fotógrafo autenticado. |
| `PUT` | `/api/auth/profile` | Atualiza perfil do fotógrafo. |
| `GET` | `/api/clientes` | Lista clientes do fotógrafo. |
| `GET` | `/api/clientes/{id}` | Busca cliente por ID. |
| `POST` | `/api/clientes` | Cria cliente. |
| `PUT` | `/api/clientes/{id}` | Atualiza cliente. |
| `DELETE` | `/api/clientes/{id}` | Remove cliente. |
| `GET` | `/api/ensaios` | Lista ensaios, com filtros opcionais `de` e `ate`. |
| `GET` | `/api/ensaios/{id}` | Busca ensaio por ID. |
| `POST` | `/api/ensaios` | Cria ensaio e contrato automático. |
| `PUT` | `/api/ensaios/{id}` | Atualiza ensaio. |
| `DELETE` | `/api/ensaios/{id}` | Remove ensaio. |
| `GET` | `/api/ensaios/dashboard` | Dados resumidos para o dashboard. |
| `GET` | `/api/contratos` | Lista contratos. |
| `POST` | `/api/contratos` | Cria ou atualiza contrato de um ensaio. |
| `PATCH` | `/api/contratos/{id}/assinar` | Marca contrato como assinado. |
| `GET` | `/api/pagamentos` | Lista pagamentos. |
| `POST` | `/api/pagamentos` | Cria pagamento. |
| `PATCH` | `/api/pagamentos/{id}/confirmar` | Marca pagamento como pago. |
| `GET` | `/api/galeria/{ensaioId}` | Lista imagens da galeria de um ensaio. |
| `POST` | `/api/galeria/{ensaioId}` | Faz upload de múltiplas imagens. |
| `DELETE` | `/api/galeria/{id}` | Remove imagem da galeria. |
| `POST` | `/api/portfolio/upload` | Faz upload de imagem do portfólio. |
| `POST` | `/api/portfolio` | Adiciona imagem ao portfólio por URL. |
| `DELETE` | `/api/portfolio/{id}` | Remove imagem do portfólio. |
| `PATCH` | `/api/portfolio/reordenar` | Reordena imagens do portfólio. |

Para rotas autenticadas, envie:

```http
Authorization: Bearer SEU_TOKEN_JWT
```

## Uploads

O serviço `FileUploadService` grava arquivos em `ClickManagerApi/wwwroot/uploads/` e retorna URLs públicas servidas pela API. Essa pasta é gerada em runtime e está ignorada pelo Git.

## Testes

Rodar todos os testes:

```bash
dotnet test ClickManager.slnx
```

Rodar apenas o projeto de testes:

```bash
dotnet test ClickManagerApi.Tests/ClickManagerApi.Tests.csproj
```

Cobertura atual dos testes:

- Funcional: autenticação, clientes, ensaios, contratos, pagamentos e galeria.
- Segurança: rotas protegidas sem token, JWT inválido, isolamento entre usuários, payloads maliciosos e health público.
- Robustez: validações, JSON inválido, corpo vazio, content type incorreto e IDs inexistentes.
- Performance: login, listagens, dashboard, criação sequencial e registros concorrentes.

Na última validação local, a suíte passou com `87` testes.

## Fluxos Importantes

Cadastro/login:

```text
Front-end -> POST /api/auth/register ou /api/auth/login -> API gera JWT -> front-end usa Bearer token nas rotas protegidas
```

Criação de ensaio:

```text
POST /api/ensaios -> cria Ensaio -> cria Contrato CTRxxxxx automaticamente
```

Contato público:

```text
POST /api/contact -> valida campos -> EmailService envia confirmação ao usuário e notificação ao AdminEmail
```

Galeria:

```text
POST /api/galeria/{ensaioId} com multipart/form-data -> salva arquivos -> cria ImagemGaleria -> incrementa TotalImagens
```

## Observações de Desenvolvimento

- A API aplica `Database.Migrate()` automaticamente quando usa PostgreSQL.
- Nos testes, o `WebFactory` troca o banco para InMemory.
- O JSON ignora propriedades nulas e evita ciclos de referência.
- O front-end ainda é estático e centraliza chamadas à API em `main.js`.
- Arquivos grandes de runtimes locais não devem entrar no repositório; use instalação local ou cache fora do Git.


