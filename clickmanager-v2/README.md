# ClickManagerApi – Back-end ASP.NET Core

Back-end da NP2 · PIM III · UNIP ADS  
Recebe o formulário do front-end via AJAX e envia e-mails com **MailKit**.

---

## 📁 Estrutura

```
ClickManagerApi/
├── Controllers/
│   └── ContactController.cs   ← POST /api/contact
├── Models/
│   ├── ContactRequest.cs      ← DTO do formulário + ApiResponse
│   └── EmailSettings.cs       ← Configurações SMTP
├── Services/
│   ├── IEmailService.cs       ← Interface
│   └── EmailService.cs        ← Implementação com MailKit
├── Properties/
│   └── launchSettings.json    ← Roda em localhost:5000
├── Program.cs                 ← DI + CORS + Pipeline
├── appsettings.json           ← ⚠️ configure seu e-mail aqui
└── ClickManagerApi.csproj
```

---

## ⚙️ Configuração (antes de rodar)

### 1. Edite o `appsettings.json`

```json
"EmailSettings": {
  "SmtpHost":        "smtp.gmail.com",
  "SmtpPort":        587,
  "SenderEmail":     "seuemail@gmail.com",

  "SenderPassword":  "SUA_SENHA_DE_APP",   ← veja abaixo
  "AdminEmail":      "seuemail@gmail.com"
}
```

### 2. Gerar Senha de App no Gmail (necessário com 2FA ativo)

1. Acesse [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)
2. Crie um app chamado **Click Manager**
3. Copie a senha de 16 caracteres e cole em `SenderPassword`

> ⚠️ **Nunca suba o `appsettings.json` com senha no GitHub!**  
> Adicione ao `.gitignore`: `appsettings.json`

---

## ▶️ Como rodar

```bash
# Restaurar pacotes
dotnet restore

# Rodar em localhost:5000
dotnet run
```

Teste rápido no navegador:
```
http://localhost:5000/api/contact/health
``
Deve retornar: `{"status":"online","timestamp":"..."}`

---

## 🔗 Fluxo Front ↔ Back

```
[Usuário preenche form]
        ↓
[main.js valida campos]
        ↓
fetch POST http://localhost:5000/api/contact
  { name, email, phone, plan, message }
        ↓
[ContactController recebe]
        ↓
[EmailService envia 2 e-mails em paralelo]
  ├── ✅ Confirmação → email do usuário
  └── 🔔 Notificação → AdminEmail
        ↓
[API retorna { success: true, message: "..." }]
        ↓
[main.js exibe alerta verde no formulário]
```

---

## 📦 Pacotes utilizados

| Pacote | Versão | Uso |
|--------|--------|-----|
| MailKit | 4.4.0 | Envio de e-mail via SMTP |
| ASP.NET Core 8 | built-in | Web API + DI + CORS |
