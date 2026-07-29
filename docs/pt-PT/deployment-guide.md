# Deployment Guide

## Configuração de Desenvolvimento Local

A sequência completa de arranque diário:

1. Abrir o Docker Desktop, depois subir o SQL Server: `docker compose up -d` (raiz do repositório) — esperar que o `gymchatai-sqlserver` fique `healthy`.
2. Confirmar que não há nenhuma migração esquecida antes de arrancar: `dotnet ef migrations has-pending-model-changes --startup-project ..\GymChatAI.Api` (a partir de `src/GymChatAI.Infrastructure`).
3. Correr a API: `dotnet run --project src\GymChatAI.Api`.
4. Numa janela de terminal separada, expor publicamente para o webhook da Meta: `ngrok http 5277`.
5. Se o URL do ngrok tiver mudado desde a última vez, atualizar o Callback URL na dashboard da app da Meta (**Use cases → Customize → Basic setup → Step 2 → Configure Webhooks**).
6. Confirmar a saúde: `GET /health`, `/health/whatsapp`, `/health/ai` devem estar todos "ok" antes de testar seja o que for à mão.
7. (Opcional) Arrancar o Portal de Administração: `cd frontend && npm run dev`.

## Modo de Persistência

Controlado inteiramente por a `ConnectionStrings:GymChatDb` estar definida ou não:

- **Presente** → modo SQL Server. As migrações aplicam-se automaticamente no arranque (`MigrateAsync()`); o ASP.NET Core Identity (e por isso a autenticação) fica ativo.
- **Ausente** → modo em memória. Não precisa de nenhuma dependência externa, mas os dados não sobrevivem a um reinício e a autenticação fica desativada — este modo existe só para exploração local sem qualquer atrito e demonstrações rápidas, nunca para nada parecido com produção.

## Configuração Única dos WhatsApp Flows (por gym)

1. Gerar um par de chaves RSA com `openssl` (2048 bits, protegido por password), guardado fora do repositório.
2. Colocar a chave privada + password nos `user-secrets` (`WhatsAppFlow:PrivateKeyPem`, `WhatsAppFlow:PrivateKeyPassword`).
3. Registar a chave pública a partir do Portal de Administração (página Definições) — isto chama automaticamente o endpoint `whatsapp_business_encryption` da Meta.
4. Definir a WABA do gym, também a partir do Portal — isto subscreve automaticamente a app para receber os eventos de webhook dessa WABA, eliminando o que costumava ser um passo manual no Graph API Explorer.

## Referência de Configuração

| Definição | De onde vem | Notas |
|---|---|---|
| `ConnectionStrings:GymChatDb` | user-secrets (dev) / configuração gerida (prod) | A presença, por si só, decide o modo de persistência |
| `WhatsApp:AccessToken` | user-secrets / configuração gerida | Usa um token permanente de System User em qualquer ambiente duradouro — um token temporário expira em ~24h |
| `WhatsAppFlow:PrivateKeyPem` / `PrivateKeyPassword` | user-secrets / cofre de segredos gerido | Nunca no `appsettings.json` |
| `AiProvider` | `appsettings.json` (não é segredo) | Override explícito; caso contrário, detetado automaticamente consoante qual chave de fornecedor está preenchida |
| `Gemini:ApiKey` / `OpenAI:ApiKey` / `AzureOpenAI:*` | user-secrets / configuração gerida | Só a chave do fornecedor ativo precisa de estar definida |

## Verificações de Saúde Antes de Testar Seja o Que For

Confirma sempre `GET /health`, `/health/whatsapp`, `/health/ai` antes de assumir um bug — uma fração enorme das sessões de "porque é que isto não funciona" durante o desenvolvimento acabaram por ser um token de WhatsApp expirado ou uma quota de fornecedor de IA esgotada, não lógica da aplicação.

## O Que Muda Numa Implementação de Produção Real

Tudo o que foi descrito acima é sobre desenvolvimento local. Uma implementação de produção difere em alguns pontos concretos:

- Sem Docker Desktop / ngrok — a base de dados é uma instância gerida de SQL Server, e a API está acessível num URL público estável e real (sem túnel, sem um URL que muda a cada reinício).
- Os segredos (token do WhatsApp, chaves dos fornecedores de IA, a chave privada RSA dos Flows) vivem num cofre de segredos gerido (ex: Azure Key Vault), em vez de `user-secrets`.
- `ASPNETCORE_ENVIRONMENT=Production` — isto, por si só, desativa os endpoints de conveniência de teste (ver `security.md`) ao nível do encaminhamento.
- O token de acesso do WhatsApp deve ser sempre um token **permanente** de System User (gerado uma vez via Meta Business Manager, "Never expire"), não o token temporário de ~24h usado para testes locais rápidos.
- O frontend do Portal de Administração é compilado (`npm run build`) e servido como um pacote estático a partir de alojamento real, em vez do servidor de desenvolvimento do Vite — e a configuração de CORS da API precisa de permitir essa origem real, não só `localhost`.
